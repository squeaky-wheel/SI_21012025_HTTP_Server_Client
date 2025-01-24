namespace Server.Auxiliary
{
    public class SingleRequestSemaphoreSlimWrapper : ISemaphore, IDisposable
    {
        private readonly SemaphoreSlim semaphore;

        public SingleRequestSemaphoreSlimWrapper()
        {
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        /// <inheritdoc/>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return this.semaphore.WaitAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public int Release()
        {
            return this.semaphore.Release();
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
        }
    }
}
