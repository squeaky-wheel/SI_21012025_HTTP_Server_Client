using Microsoft.Extensions.Options;
using Server.Auxiliary;
using Server.Options;
using System.Net.WebSockets;
using System.Text;

namespace Server.ClientNotifications
{
    public class ClientNotificationService : IClientNotificationService
    {
        private WebSocket socket;
        private readonly byte[] receiveBuffer;
        // Only one send and one receive is supported on a WebSocket object at a give moment in time.
        // Hence the usage of the synchronisation mechanisms.
        private readonly ISingleRequestSemaphore txSemaphore;
        private readonly ISingleRequestSemaphore rxSemaphore;

        public ClientNotificationService(
            ISingleRequestSemaphore txSemaphore,
            ISingleRequestSemaphore rxSemaphore,
            IOptions<ClientNotificationServiceSettings> wrappedSettings
            )
        {
            if (txSemaphore is null)
                throw new ArgumentNullException(nameof(txSemaphore));

            if (rxSemaphore is null)
                throw new ArgumentNullException(nameof(rxSemaphore));

            if (ReferenceEquals(txSemaphore, rxSemaphore))
                throw new ArgumentException("The Tx and Rx semaphores are the same.");

            this.txSemaphore = txSemaphore;
            this.rxSemaphore = rxSemaphore;

            if (wrappedSettings is null)
                throw new ArgumentNullException(nameof(wrappedSettings));

            if (wrappedSettings.Value is null)
                throw new ArgumentException("The settings are absent from the settings wrapper.");

            this.receiveBuffer = new byte[wrappedSettings.Value.ReceiveBufferSizeBytes.Value];
        }

        /// <inheritdoc/>
        public async Task SetSocketAsync(WebSocket socketToSet, CancellationToken cancellationToken)
        {
            if (socketToSet is null)
                throw new ArgumentNullException(nameof(socketToSet));

            bool txSemaphoreAcquired = false;
            bool rxSemaphoreAcquired = false;

            try
            {
                await txSemaphore.WaitAsync(cancellationToken);
                txSemaphoreAcquired = true;

                await rxSemaphore.WaitAsync(cancellationToken);
                rxSemaphoreAcquired = true;

                if (this.socket is not null)
                    throw new InvalidOperationException("The service already has a socket.");

                this.socket = socketToSet;
            }
            finally
            {
                if (txSemaphoreAcquired)
                    txSemaphore.Release();

                if (rxSemaphoreAcquired)
                    rxSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task RemoveSocketAsync()
        {
            bool txSemaphoreAcquired = false;
            bool rxSemaphoreAcquired = false;

            try
            {
                await txSemaphore.WaitAsync(default);
                txSemaphoreAcquired = true;

                await rxSemaphore.WaitAsync(default);
                rxSemaphoreAcquired = true;

                if (this.socket is null)
                    throw new InvalidOperationException("Can not remove an absent socket.");

                this.socket = null;
            }
            finally
            {
                if (txSemaphoreAcquired)
                    txSemaphore.Release();

                if (rxSemaphoreAcquired)
                    rxSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> WaitForClosureRequestAsync(CancellationToken cancellationToken)
        {
            bool rxSemaphoreAcquired = false;
            bool txSemaphoreAcquired = false;

            try
            {
                await rxSemaphore.WaitAsync(cancellationToken);
                rxSemaphoreAcquired = true;

                WebSocketReceiveResult receiveResult;
                do
                {
                    if (!GetIsWebSocketReadyForCommunication())
                        return false;

                    receiveResult = await this.socket.ReceiveAsync(receiveBuffer, cancellationToken);
                    if (receiveResult is null)
                        throw new WebSocketException($"{nameof(WebSocketReceiveResult)} is absent.");
                    // ReceiveAsync() keeps waiting for a message to arrive.
                    // So there is no need to Task.Delay() the loop to save the CPU cycles.
                } while (receiveResult.MessageType != WebSocketMessageType.Close);

                await txSemaphore.WaitAsync(cancellationToken);
                txSemaphoreAcquired = true;

                await this.socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    null,
                    cancellationToken
                    );

                return true;
            }
            finally
            {
                if (rxSemaphoreAcquired)
                    rxSemaphore.Release();

                if (txSemaphoreAcquired)
                    txSemaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> TransmitAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException(
                    "The text is either absent, empty or consists of white-space only.", nameof(text)
                    );

            // WebSockets' specification demands TEXT payload to be UTF-8 encoded.
            var utf8Bytes = Encoding.UTF8.GetBytes(text);

            await txSemaphore.WaitAsync(cancellationToken);

            try
            {
                if (!GetIsWebSocketReadyForCommunication())
                    return false;

                await this.socket.SendAsync(
                    utf8Bytes,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken
                    );

                return true;
            }
            finally
            {
                txSemaphore.Release();
            }
        }

        /// <summary>
        /// Operates on <see cref="socket"/>.
        /// The caller MUST ensure serial access via AT LEAST ONE of the synchronisation mechanisms.
        /// </summary>
        private bool GetIsWebSocketReadyForCommunication()
        {
            if (this.socket is null)
                return false;

            if (this.socket.State != WebSocketState.Open)
                return false;

            return true;
        }
    }
}
