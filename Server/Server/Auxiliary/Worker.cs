using Microsoft.Extensions.Options;
using Server.Options;

namespace Server.Auxiliary
{
    public class Worker : IWorker
    {
        private readonly WorkerSettings settings;

        public Worker(IOptions<WorkerSettings> wrappedSettings)
        {
            if (wrappedSettings is null)
                throw new ArgumentNullException(nameof(wrappedSettings));

            if (wrappedSettings.Value is null)
                throw new ArgumentException("The settings are absent from the settings wrapper.");

            this.settings = wrappedSettings.Value;
        }

        public Task PretendToWorkAsync(CancellationToken cancellationToken)
        {
            return Task.Delay(this.settings.TimeToPretendToWorkForMilliseconds.Value, cancellationToken);
        }
    }
}
