using System.Net.WebSockets;

namespace Server.ClientNotifications
{
    public interface IClientNotificationService
    {
        /// <summary>
        /// Sets a socket for use by the service. The socket has to be remove using <see cref="RemoveSocketAsync"/>
        /// once no longer needed. The service can have only one socket set at a given time.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="socketToSet"/> is absent.</exception>
        /// <exception cref="OperationCanceledException">When socket setting has been cancelled.</exception>
        /// <exception cref="InvalidOperationException">When the service already has a socket.</exception>
        /// <exception cref="ObjectDisposedException">When the service has been disposed.</exception>
        Task SetSocketAsync(WebSocket socketToSet, CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to remove a socket.
        /// If one is set - it is removed from the service.
        /// It is an error to attempt a removal of a socket, which was not set.
        /// A socket must have been set using <see cref="SetSocketAsync"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">When an attempt is made to remove an absent socket.</exception>
        /// <exception cref="ObjectDisposedException">When the service has been disposed.</exception>
        Task RemoveSocketAsync();

        /// <returns><see cref="true"/> if the closure request was received;
        /// <see cref="false"/> if the client is not ready for communication.</returns>
        /// <exception cref="WebSocketException">When an attempt to receive a closure request resulted in
        /// an unexpected boundary condition.</exception>
        /// <exception cref="OperationCanceledException">When the wait has been cancelled.</exception>
        /// <exception cref="ObjectDisposedException">When the service has been disposed.</exception>
        Task<bool> WaitForClosureRequestAsync(CancellationToken cancellationToken);

        /// <returns><see cref="true"/> if the <paramref name="text"/> was transmitted;
        /// <see cref="false"/> if the client is not ready for communication.</returns>
        /// <exception cref="ArgumentException">When <paramref name="text"/> is either absent,
        /// empty or consists of white-space only..</exception>
        /// <exception cref="OperationCanceledException">When the trasmission has been cancelled.</exception>
        /// <exception cref="ObjectDisposedException">When the service has been disposed.</exception>
        Task<bool> TransmitAsync(string text, CancellationToken cancellationToken);
    }
}