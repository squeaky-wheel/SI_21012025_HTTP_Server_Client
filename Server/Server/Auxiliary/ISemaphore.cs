
namespace Server.Auxiliary
{
    public interface ISemaphore
    {
        /// <summary>
        /// Asynchronously waits to enter the <see cref="SemaphoreSlim"/>.
        /// </summary>
        /// <returns>A task that will complete when the semaphore has been entered.</returns>
        Task WaitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Exits the <see cref="SemaphoreSlim"/> once.
        /// </summary>
        /// <returns>The previous count of the <see cref="SemaphoreSlim"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been
        /// disposed.</exception>
        int Release();
    }
}