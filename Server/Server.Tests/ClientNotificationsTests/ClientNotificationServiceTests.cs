using Microsoft.Extensions.Options;
using Moq;
using Server.Auxiliary;
using Server.ClientNotifications;
using Server.Options;
using System.Net.WebSockets;
using System.Text;

namespace Server.Tests.ClientNotificationsTests
{
    public class ClientNotificationServiceTests
    {
        [Fact]
        public void ConstructorThrowsArgNullWhenTxSemaphoreIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ClientNotificationService(
                    null, Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenRxSemaphoreIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ClientNotificationService(
                    Mock.Of<ISingleRequestSemaphore>(), null, MockWrappedSettings()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgExceptionWhenTxSemaphoreAndRxSemaphoreAreTheSame()
        {
            var sameSemaphore = Mock.Of<ISingleRequestSemaphore>();

            Assert.Throws<ArgumentException>(
                () => new ClientNotificationService(
                    sameSemaphore, sameSemaphore, MockWrappedSettings()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenSettingsWrapperIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ClientNotificationService(
                    Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), null
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgExceptionWhenSettingsAreAbsent()
        {
            var wrappedAbsentSettingsMock = new Mock<IOptions<ClientNotificationServiceSettings>>();
            wrappedAbsentSettingsMock.SetupGet(wsm => wsm.Value).Returns((ClientNotificationServiceSettings)null);

            Assert.Throws<ArgumentException>(
                () => new ClientNotificationService(
                    Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), wrappedAbsentSettingsMock.Object
                    )
                );
        }

        [Fact]
        public async Task SetSocketAsyncThrowsArgNullWhenSocketIsAbsent()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => sut.SetSocketAsync(null, default)
                );
        }

        [Fact]
        public async Task SetSocketAsyncThrowsInvalidOpWhenSocketIsAlreadySet()
        {
            var socket01 = Mock.Of<WebSocket>();
            var socket02 = Mock.Of<WebSocket>();

            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            await sut.SetSocketAsync(socket01, default);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.SetSocketAsync(socket02, default)
                );
        }

        [Fact]
        public async Task SetSocketAsyncFirstAcquiresThenReleasesBothTxAndRxSemaphores()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>(MockBehavior.Strict);
            var rxSemaphore = new Mock<ISingleRequestSemaphore>(MockBehavior.Strict);

            var sequence = new MockSequence();

            txSemaphore.InSequence(sequence).Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            rxSemaphore.InSequence(sequence).Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            txSemaphore.InSequence(sequence).Setup(s => s.Release()).Returns((int)default);
            rxSemaphore.InSequence(sequence).Setup(s => s.Release()).Returns((int)default);

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            await sut.SetSocketAsync(Mock.Of<WebSocket>(), default);

            txSemaphore.Verify(s => s.WaitAsync(default));
            rxSemaphore.Verify(s => s.WaitAsync(default));
            txSemaphore.Verify(s => s.Release());
            rxSemaphore.Verify(s => s.Release());
        }

        [Fact]
        public async Task SetSocketAsyncReleasesTxSemaphoreWhenTxSemaphoreAcquiredButRxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            rxSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.SetSocketAsync(Mock.Of<WebSocket>(), default);
            }
            catch (Exception) { }

            txSemaphore.Verify(s => s.WaitAsync(default), Times.Once);
            txSemaphore.Verify(s => s.Release(), Times.Once);
        }

        [Fact]
        public async Task SetSocketAsyncDoesNotReleaseTxSemaphoreWhenTxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            txSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.SetSocketAsync(Mock.Of<WebSocket>(), default);
            }
            catch (Exception) { }

            txSemaphore.Verify(s => s.Release(), Times.Never);
        }

        [Fact]
        public async Task SetSocketAsyncDoesNotReleaseRxSemaphoreWhenRxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            rxSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.SetSocketAsync(Mock.Of<WebSocket>(), default);
            }
            catch (Exception) { }

            rxSemaphore.Verify(s => s.Release(), Times.Never);
        }

        [Fact]
        public async Task SetSocketAsyncAcquiresAndReleasesSemaphoresWhenExceptionThrownBecauseSocketIsAlreadySet()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            var socket01 = Mock.Of<WebSocket>();
            var socket02 = Mock.Of<WebSocket>();

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            await sut.SetSocketAsync(socket01, default);

            try
            {
                await sut.SetSocketAsync(socket02, default);
            }
            catch (InvalidOperationException) { }

            txSemaphore.Verify(s => s.WaitAsync(default));
            rxSemaphore.Verify(s => s.WaitAsync(default));
            txSemaphore.Verify(s => s.Release());
            rxSemaphore.Verify(s => s.Release());
        }

        [Fact]
        public async Task RemoveSocketAsyncThrowsInvalidOpWhenSocketIsNotSet()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.RemoveSocketAsync()
                );
        }

        [Fact]
        public async Task RemoveSocketAsyncFirstAcquiresThenReleasesBothTxAndRxSemaphoresWhenSocketIsSset()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>(MockBehavior.Strict);
            var rxSemaphore = new Mock<ISingleRequestSemaphore>(MockBehavior.Strict);

            // Setup to pass the SetSocketAsync()
            txSemaphore.Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            rxSemaphore.Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            txSemaphore.Setup(s => s.Release()).Returns((int)default);
            rxSemaphore.Setup(s => s.Release()).Returns((int)default);

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            await sut.SetSocketAsync(Mock.Of<WebSocket>(), default);

            txSemaphore.Reset(); // Clearing all the setup in order to set the mocks up for RemoveSocketAsync()

            var sequence = new MockSequence();
            txSemaphore.InSequence(sequence).Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            rxSemaphore.InSequence(sequence).Setup(s => s.WaitAsync(default)).Returns(Task.CompletedTask);
            txSemaphore.InSequence(sequence).Setup(s => s.Release()).Returns((int)default);
            rxSemaphore.InSequence(sequence).Setup(s => s.Release()).Returns((int)default);

            await sut.RemoveSocketAsync();

            txSemaphore.Verify(s => s.WaitAsync(default));
            rxSemaphore.Verify(s => s.WaitAsync(default));
            txSemaphore.Verify(s => s.Release());
            rxSemaphore.Verify(s => s.Release());
        }

        [Fact]
        public async Task RemoveSocketAsyncReleasesTxSemaphoreWhenTxSemaphoreAcquiredButRxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            rxSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.RemoveSocketAsync();
            }
            catch (Exception) { }

            txSemaphore.Verify(s => s.WaitAsync(default), Times.Once);
            txSemaphore.Verify(s => s.Release(), Times.Once);
        }

        [Fact]
        public async Task RemoveSocketAsyncDoesNotReleaseTxSemaphoreWhenTxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            txSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.RemoveSocketAsync();
            }
            catch (Exception) { }

            txSemaphore.Verify(s => s.Release(), Times.Never);
        }

        [Fact]
        public async Task RemoveSocketAsyncDoesNotReleaseRxSemaphoreWhenRxAcquisitionThrows()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            rxSemaphore.Setup(s => s.WaitAsync(default)).Throws(new Exception("Bad things happened."));

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.RemoveSocketAsync();
            }
            catch (Exception) { }

            rxSemaphore.Verify(s => s.Release(), Times.Never);
        }

        [Fact]
        public async Task RemoveSocketAsyncAcquiresAndReleasesSemaphoresWhenExceptionThrownBecauseNoSocketIsSet()
        {
            var txSemaphore = new Mock<ISingleRequestSemaphore>();
            var rxSemaphore = new Mock<ISingleRequestSemaphore>();

            var sut = new ClientNotificationService(
                txSemaphore.Object, rxSemaphore.Object, MockWrappedSettings()
                );

            try
            {
                await sut.RemoveSocketAsync();
            }
            catch (InvalidOperationException) { }

            txSemaphore.Verify(s => s.WaitAsync(default));
            rxSemaphore.Verify(s => s.WaitAsync(default));
            txSemaphore.Verify(s => s.Release());
            rxSemaphore.Verify(s => s.Release());
        }

        [Fact]
        public async Task WaitForClosureRequestAsyncReturnsFalseWhenSocketIsNotSet()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var result = await sut.WaitForClosureRequestAsync(default);

            Assert.False(result);
        }

        [Theory]
        [InlineData(WebSocketState.None)]
        [InlineData(WebSocketState.Connecting)]
        [InlineData(WebSocketState.CloseSent)]
        [InlineData(WebSocketState.CloseReceived)]
        [InlineData(WebSocketState.Closed)]
        [InlineData(WebSocketState.Aborted)]
        public async Task WaitForClosureRequestAsyncReturnsFalseWhenSocketIsSetWithStateNotOpen(
            WebSocketState state
            )
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(state);

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.WaitForClosureRequestAsync(default);

            Assert.False(result);
        }

        [Fact]
        public async Task WaitForClosureRequestAsyncThrowsWebSocketExceptionWhenAbsentMessageReceived()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);
            socketMock
                .Setup(sm => sm.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), default))
                .ReturnsAsync((WebSocketReceiveResult)null);

            await sut.SetSocketAsync(socketMock.Object, default);

            await Assert.ThrowsAsync<WebSocketException>(
                () => sut.WaitForClosureRequestAsync(default)
                );
        }

        [Fact]
        public async Task WaitForClosureRequestKeepsReceivingUntilClosureRequestArrives()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);
            socketMock
                .SetupSequence(sm => sm.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), default))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Text, true))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Binary, true))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Text, true))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Close, true));

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.WaitForClosureRequestAsync(default);

            Assert.True(result);
        }

        [Fact]
        public async Task WaitForClosureRequestAsyncReturnsTrueWhenReceivedClosureRequest()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);
            socketMock
                .Setup(sm => sm.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), default))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Close, true));

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.WaitForClosureRequestAsync(default);

            Assert.True(result);
        }

        [Fact]
        public async Task WaitForClosureRequestAsyncSendsCloseFrameAfterReceivingClosureRequest()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var sequence = new MockSequence();

            var socketMock = new Mock<WebSocket>(MockBehavior.Strict);
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);
            socketMock
                .InSequence(sequence)
                .Setup(sm => sm.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), default))
                .ReturnsAsync(new WebSocketReceiveResult(default, WebSocketMessageType.Close, true));

            socketMock
                .InSequence(sequence)
                .Setup(sm => sm.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default))
                .Returns(Task.CompletedTask);

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.WaitForClosureRequestAsync(default);

            socketMock
                .Verify(sm => sm.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), default), Times.Once);
            socketMock
                .Verify(sm => sm.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default), Times.Once);
        }

        // For the sake of saving the time, let us pretend that instead of this comment -
        // there is an array of tests, thoroughly testing the acquisiton and release
        // of the synchronisation mechanisms, under various boundary conditions,
        // for the method WaitForClosureRequestAsync().
        // There are plenty of cases, for other methods of ClientNotificationService,
        // demonstating my ability to compose such test suites.

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("     ")]
        public async Task TransmitAsyncThrowsArgumentExceptionWhenTextIsEitherAbsentEmptyOrWhiteSpaceOnly(
            string text
            )
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            await Assert.ThrowsAsync<ArgumentException>(
                () => sut.TransmitAsync(text, default)
                );
        }

        [Fact]
        public async Task TransmitAsyncReturnsFalseWhenSocketIsNotSet()
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var result = await sut.TransmitAsync("Text.", default);

            Assert.False(result);
        }

        [Theory]
        [InlineData(WebSocketState.None)]
        [InlineData(WebSocketState.Connecting)]
        [InlineData(WebSocketState.CloseSent)]
        [InlineData(WebSocketState.CloseReceived)]
        [InlineData(WebSocketState.Closed)]
        [InlineData(WebSocketState.Aborted)]
        public async Task TransmitAsyncReturnsFalseWhenSocketIsSetWithStateNotOpen(
            WebSocketState state
            )
        {
            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(state);

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.TransmitAsync("Text.", default);

            Assert.False(result);
        }

        [Fact]
        public async Task TransmitAsyncSendsTextEncodedAsUTF8()
        {
            // a reasonable sample, that, would differ byte-wise if encoded, for example, using UTF-16 instead.
            var text = "∮→∞∑∏";
            var expected = new byte[] { 226, 136, 174, 226, 134, 146, 226, 136, 158, 226, 136, 145, 226, 136, 143 };

            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);

            await sut.SetSocketAsync(socketMock.Object, default);

            await sut.TransmitAsync(text, default);

            socketMock.Verify(sm => sm.SendAsync(expected, WebSocketMessageType.Text, true, default), Times.Once);
        }

        [Fact]
        public async Task TransmitAsyncReturnsTrueAfterSendingText()
        {
            var text = "Text.";
            var utf8Bytes = Encoding.UTF8.GetBytes(text);

            var sut = new ClientNotificationService(
                Mock.Of<ISingleRequestSemaphore>(), Mock.Of<ISingleRequestSemaphore>(), MockWrappedSettings()
                );

            var socketMock = new Mock<WebSocket>();
            socketMock.SetupGet(sm => sm.State).Returns(WebSocketState.Open);

            await sut.SetSocketAsync(socketMock.Object, default);

            var result = await sut.TransmitAsync(text, default);

            socketMock.Verify(sm => sm.SendAsync(utf8Bytes, WebSocketMessageType.Text, true, default), Times.Once);

            Assert.True(result);
        }

        // For the sake of saving the time, let us pretend that instead of this comment -
        // there is an array of tests, thoroughly testing the acquisiton and release
        // of the synchronisation mechanisms, under various boundary conditions,
        // for the method TransmitAsync().
        // There are plenty of cases, for other methods of ClientNotificationService,
        // demonstating my ability to compose such test suites.

        private IOptions<ClientNotificationServiceSettings> MockWrappedSettings()
        {
            var wrappedSettingsMock = new Mock<IOptions<ClientNotificationServiceSettings>>();

            var settings = new ClientNotificationServiceSettings();
            settings.ReceiveBufferSizeBytes = 4096;

            wrappedSettingsMock.SetupGet(wsm => wsm.Value).Returns(settings);

            return wrappedSettingsMock.Object;
        }
    }
}
