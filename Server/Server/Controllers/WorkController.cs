using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Server.Auxiliary;
using Server.ClientNotifications;

namespace Server.Controllers
{
    [Route("work")]
    [ApiController]
    public class WorkController : ControllerBase
    {
        private readonly IClientNotificationService clientNotificationService;
        private readonly IGuidProvider guidProvider;
        private readonly IWorker worker;
        private readonly ILogger<WorkController> logger;

        public WorkController(
            IClientNotificationService clientNotificationService,
            IGuidProvider guidProvider,
            IWorker worker,
            ILogger<WorkController> logger
            )
        {
            this.clientNotificationService = clientNotificationService
                ?? throw new ArgumentNullException(nameof(clientNotificationService));

            this.guidProvider = guidProvider
                ?? throw new ArgumentNullException(nameof(guidProvider));

            this.worker = worker
                ?? throw new ArgumentNullException(nameof(worker));

            this.logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [Route("start")]
        [EnableRateLimiting(RateLimiterPolicyNames.WorkStartRateLimiter)]
        public async Task<IActionResult> StartAsync(CancellationToken cancellationToken)
        {
            bool transmissionResult = false;

            try
            {
                var workGuid = this.guidProvider.GetGuid();

                transmissionResult =
                    await this.clientNotificationService.TransmitAsync(
                        $"{Messages.WorkStartedNotification} {workGuid}",
                        cancellationToken
                        );
                if (!transmissionResult)
                {
                    this.logger.LogError(
                        "Tried transmitting a notification about the work being started, " +
                        "however, the client was not ready for communication."
                        );
                    return StatusCode(StatusCodes.Status400BadRequest, HTTPResponses.ClientNotListening);
                }

                await this.worker.PretendToWorkAsync(cancellationToken);

                transmissionResult =
                    await this.clientNotificationService.TransmitAsync(
                        $"{Messages.WorkCompletedNotification} {workGuid}",
                        cancellationToken
                        );
                if (!transmissionResult)
                {
                    this.logger.LogError(
                        "Tried transmitting a notification about the work being completed, " +
                        "however, the client was not ready for communication."
                        );
                    return StatusCode(StatusCodes.Status400BadRequest, HTTPResponses.ClientNotListening);
                }
            }
            catch (OperationCanceledException exception)
            {
                this.logger.LogWarning(exception, "Work start request cancelled.");
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
