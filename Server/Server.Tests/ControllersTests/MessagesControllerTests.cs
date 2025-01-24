using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Server.ClientNotifications;
using Server.Controllers;
using Server.Options;
using System.Net.WebSockets;

namespace Server.Tests.ControllersTests
{
    public class MessagesControllerTests
    {
        [Fact]
        public void ConstructorThrowsArgNullWhenClientNotificationServiceIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new MessagesController(
                    null,
                    MockWrappedSettings(),
                    Mock.Of<ILogger<MessagesController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenSettingsWrapperIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new MessagesController(
                    Mock.Of<IClientNotificationService>(),
                    null,
                    Mock.Of<ILogger<MessagesController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgExceptionWhenSettingsAreAbsentFromTheWrapper()
        {
            var wrapperWithoutSettingsMock = new Mock<IOptions<MessagesSettings>>();
            wrapperWithoutSettingsMock.SetupGet(wwsm => wwsm.Value).Returns((MessagesSettings)null);

            Assert.Throws<ArgumentException>(
                () => new MessagesController(
                    Mock.Of<IClientNotificationService>(),
                    wrapperWithoutSettingsMock.Object,
                    Mock.Of<ILogger<MessagesController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenLoggerIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new MessagesController(
                    Mock.Of<IClientNotificationService>(),
                    MockWrappedSettings(),
                    null
                    )
                );
        }

        [Fact]
        public async Task OpenConnectionAsyncSetsResponseStatusTo400WhenRequestIsNotWebSocketOne()
        {
            var sut = new MessagesController(
                Mock.Of<IClientNotificationService>(), MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(false);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            await sut.OpenConnectionAsync(default);

            Assert.Equal(StatusCodes.Status400BadRequest, contextMock.Object.Response.StatusCode);
        }

        [Fact]
        public async Task OpenConnectionAsyncDoesNotInteractWithClientNotificationServiceWhenRequestIsNotWebSocketOne()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>(MockBehavior.Strict);

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(false);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task OpenConnectionAsyncSetsResponseStatusTo400WhenWhenWebSocketManagerIsAbsent()
        {
            var sut = new MessagesController(
                Mock.Of<IClientNotificationService>(), MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns((WebSocketManager)null);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            await sut.OpenConnectionAsync(default);

            Assert.Equal(StatusCodes.Status400BadRequest, contextMock.Object.Response.StatusCode);
        }

        [Fact]
        public async Task OpenConnectionAsyncDoesNotThrowWhenHttpContextIsAbsent()
        {
            var sut = new MessagesController(
                Mock.Of<IClientNotificationService>(), MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            sut.ControllerContext.HttpContext = null;

            var exception = await Record.ExceptionAsync(() => sut.OpenConnectionAsync(default));

            Assert.Null(exception);
        }

        [Fact]
        public async Task OpenConnectionAsyncDoesNotThrowWhenNotWebSocketRequestAndHttpResponseIsAbsent()
        {
            var sut = new MessagesController(
                Mock.Of<IClientNotificationService>(), MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(false);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns((HttpResponse)null);

            sut.ControllerContext.HttpContext = contextMock.Object;

            var exception = await Record.ExceptionAsync(() => sut.OpenConnectionAsync(default));
            
            Assert.Null(exception);
        }

        [Fact]
        public async Task OpenConnectionAsyncSetsAcceptedWebsocket()
        {
            var webSocket = Mock.Of<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocket);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.Verify(cnsm => cnsm.SetSocketAsync(webSocket, default), Times.Once);
        }

        /// <summary>
        /// The designed workflow is:
        ///     Set the accepted WebSocket.
        ///     Trasmit the connection response (such as 'Welcome')
        ///     Wait for the Close payload from a client
        ///     Remove the socket
        /// </summary>
        [Fact]
        public async Task OpenConnectionAsyncExecutesTheDesignedWorkflowInSequence()
        {
            var webSocket = Mock.Of<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>(MockBehavior.Strict);

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocket);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            var sequence = new MockSequence();

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.SetSocketAsync(webSocket, default))
                .Returns(Task.CompletedTask);

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default))
                .ReturnsAsync(true);

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.WaitForClosureRequestAsync(default))
                .ReturnsAsync(true);

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.RemoveSocketAsync())
                .Returns(Task.CompletedTask);

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.SetSocketAsync(webSocket, default), Times.Once);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default), Times.Once);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.WaitForClosureRequestAsync(default), Times.Once);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.RemoveSocketAsync(), Times.Once);
        }

        [Fact]
        public async Task OpenConnectionAsyncDisposesAcceptedWebSocketOnlyAfterRemovalFromNotificationService()
        {
            var webSocketMock = new Mock<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>(MockBehavior.Strict);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.SetSocketAsync(webSocketMock.Object, default))
                .Returns(Task.CompletedTask);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default))
                .ReturnsAsync(true);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.WaitForClosureRequestAsync(default))
                .ReturnsAsync(true);

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocketMock.Object);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            var sequence = new MockSequence();

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.RemoveSocketAsync())
                .Returns(Task.CompletedTask);

            webSocketMock.InSequence(sequence)
                .Setup(wsm => wsm.Dispose());

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.Verify(cnsm => cnsm.RemoveSocketAsync(), Times.Once);

            webSocketMock.Verify(wsm => wsm.Dispose(), Times.Once);
        }

        [Fact]
        public async Task OpenConnectionAsyncDoesNotRemoveSocketIfSetSocketThrows()
        {
            var webSocketMock = new Mock<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.SetSocketAsync(webSocketMock.Object, default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocketMock.Object);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.Verify(cnsm => cnsm.RemoveSocketAsync(), Times.Never);
        }

        [Fact]
        public async Task OpenConnectionAsyncRemovesSocketIfTransmissionAttemptThrows()
        {
            var webSocket = Mock.Of<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocket);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            var sequence = new MockSequence();

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.Verify(cnsm => cnsm.RemoveSocketAsync(), Times.Once);
        }

        [Fact]
        public async Task OpenConnectionAsyncRemovesSocketIfExceptionThrownWhileWaitingForClosure()
        {
            var webSocket = Mock.Of<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default))
                .ReturnsAsync(true);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.WaitForClosureRequestAsync(default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocket);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            var sequence = new MockSequence();

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock.Verify(cnsm => cnsm.RemoveSocketAsync(), Times.Once);
        }

        [Fact]
        public async Task OpenConnectionAsyncDoesNotWaitForClosureIfTramissionFails()
        {
            var webSocket = Mock.Of<WebSocket>();

            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ClientConnectedResponse, default))
                .ReturnsAsync(false);

            var sut = new MessagesController(
                clientNotificationServiceMock.Object, MockWrappedSettings(), Mock.Of<ILogger<MessagesController>>()
                );

            var webSocketManagerMock = new Mock<WebSocketManager>();
            webSocketManagerMock.SetupGet(wsmm => wsmm.IsWebSocketRequest).Returns(true);
            webSocketManagerMock
                .Setup(wsmm => wsmm.AcceptWebSocketAsync(It.IsAny<WebSocketAcceptContext>()))
                .ReturnsAsync(webSocket);

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(cm => cm.WebSockets).Returns(webSocketManagerMock.Object);
            contextMock.SetupGet(cm => cm.Response).Returns(Mock.Of<HttpResponse>());

            sut.ControllerContext.HttpContext = contextMock.Object;

            var sequence = new MockSequence();

            await sut.OpenConnectionAsync(default);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.WaitForClosureRequestAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        private IOptions<MessagesSettings> MockWrappedSettings()
        {
            var wrappedSettingsMock = new Mock<IOptions<MessagesSettings>>();

            var settings = new MessagesSettings();
            settings.WebSocketConnectionKeepAliveIntervalMilliseconds = 500;

            wrappedSettingsMock.SetupGet(wsm => wsm.Value).Returns(settings);

            return wrappedSettingsMock.Object;
        }
    }
}
