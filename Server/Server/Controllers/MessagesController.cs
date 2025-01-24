using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Server.Auxiliary;
using Server.ClientNotifications;
using Server.Options;
using System.Net.WebSockets;

namespace Server.Controllers
{
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IClientNotificationService clientNotificationService;
        private readonly ILogger<MessagesController> logger;
        private readonly WebSocketAcceptContext webSocketAcceptContext;

        public MessagesController(
            IClientNotificationService clientNotificationService,
            IOptions<MessagesSettings> wrappedSettings,
            ILogger<MessagesController> logger
            )
        {
            if (clientNotificationService is null)
                throw new ArgumentNullException(nameof(clientNotificationService));

            this.clientNotificationService = clientNotificationService;

            if (wrappedSettings is null)
                throw new ArgumentNullException(nameof(wrappedSettings));

            if (wrappedSettings.Value is null)
                throw new ArgumentException("The settings are absent from the settings wrapper.");

            webSocketAcceptContext = new WebSocketAcceptContext();
            webSocketAcceptContext.KeepAliveInterval =
                TimeSpan.FromMilliseconds(
                    wrappedSettings.Value.WebSocketConnectionKeepAliveIntervalMilliseconds.Value
                    );

            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            this.logger = logger;
        }

        [HttpGet]
        [Route("messages")]
        [EnableRateLimiting(RateLimiterPolicyNames.MessagesRateLimiter)]
        public async Task OpenConnectionAsync(CancellationToken cancellationToken)
        {
            bool isSocketSet = false;

            try
            {
                var isWebSocketRequest = HttpContext?.WebSockets?.IsWebSocketRequest;

                if (isWebSocketRequest.HasValue ? !isWebSocketRequest.Value : true)
                {
                    this.logger.LogWarning("Non-WebSocket request. The endpoint supports only WebSocket requests.");

                    if (HttpContext is null)
                    {
                        this.logger.LogCritical("The HTTP context is absent.");
                        return;
                    }

                    if (HttpContext.Response is null)
                    {
                        this.logger.LogCritical("The HTTP contexts' response is absent.");
                        return;
                    }

                    HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(this.webSocketAcceptContext);

                await this.clientNotificationService.SetSocketAsync(webSocket, cancellationToken);
                isSocketSet = true;

                var transmissionResult =
                    await this.clientNotificationService.TransmitAsync(
                        Messages.ClientConnectedResponse, cancellationToken
                        );
                if (!transmissionResult)
                {
                    throw new WebSocketException(
                        "Tried transmitting a client connection response, " +
                        "however, the client was not ready for communication."
                        );
                }

                var closureRequestWaitResult =
                    await this.clientNotificationService.WaitForClosureRequestAsync(cancellationToken);
                if (!closureRequestWaitResult)
                {
                    throw new WebSocketException(
                        "Tried awaiting a closure request, " +
                        "however, the client was not ready for communication."
                        );
                }
            }
            catch (OperationCanceledException exception)
            {
                this.logger.LogWarning(exception, "Websocket connection cancelled.");
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "An unexpected exception has occured.");
            }
            finally
            {
                try
                {
                    // Need to remove the reference to the WebSocket,
                    // which will be disposed of at the end of the scope of this method.
                    // And it has to be done in the cases where the exceptions have been thrown, by the code above, too.
                    if (isSocketSet)
                        await this.clientNotificationService.RemoveSocketAsync();
                }
                catch (Exception exception)
                {
                    this.logger.LogError(exception, "An unexpected exception has occured.");
                }
            }
        }
    }
}
