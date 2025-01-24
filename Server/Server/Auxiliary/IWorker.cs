
namespace Server.Auxiliary
{
    public interface IWorker
    {
        /// <exception cref="OperationCanceledException">When the 'work' has been cancelled.</exception>
        Task PretendToWorkAsync(CancellationToken cancellationToken);
    }
}