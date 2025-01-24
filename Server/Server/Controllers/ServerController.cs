using Microsoft.AspNetCore.Mvc;
using Server.ClientNotifications;

namespace Server.Controllers
{
    [Route("server")]
    [ApiController]
    public class ServerController : ControllerBase
    {
        private readonly IClientNotificationService clientNotificationService;
        private readonly ILogger<ServerController> logger;

        public ServerController(
            IClientNotificationService clientNotificationService,
            ILogger<ServerController> logger
            )
        {
            this.clientNotificationService = clientNotificationService
                ?? throw new ArgumentNullException(nameof(clientNotificationService));

            this.logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [Route("ping")]
        public async Task<IActionResult> PingAsync(CancellationToken cancellationToken)
        {
            try
            {
                var transmissionResult =
                    await this.clientNotificationService.TransmitAsync(Messages.ServerPingResponse, cancellationToken);

                if (!transmissionResult)
                {
                    this.logger.LogError("Received a ping request, however, the client is not ready for communication.");
                    return StatusCode(StatusCodes.Status400BadRequest, HTTPResponses.ClientNotListening);
                }
            }
            catch (OperationCanceledException exception)
            {
                this.logger.LogWarning(exception, "Ping request cancelled.");
                return StatusCode(StatusCodes.Status408RequestTimeout);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "An unexpected exception has occured.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }
    }
}
