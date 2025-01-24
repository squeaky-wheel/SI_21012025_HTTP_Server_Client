using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Server.ClientNotifications;
using Server.Controllers;

namespace Server.Tests.ControllersTests
{
    public class ServerControllerTests
    {
        [Fact]
        public void ConstructorThrowsArgNullWhenClientNotificationServiceIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ServerController(
                    null,
                    Mock.Of<ILogger<ServerController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenLoggerIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ServerController(
                    Mock.Of<IClientNotificationService>(),
                    null
                    )
                );
        }

        [Fact]
        public async Task PingAsyncTransmitsPingRequestResponse()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            var sut = new ServerController(
                clientNotificationServiceMock.Object, Mock.Of<ILogger<ServerController>>()
                );

            await sut.PingAsync(default);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.TransmitAsync(Messages.ServerPingResponse, default), Times.Once);
        }

        [Fact]
        public async Task PingAsyncReturnsStatus200WhenTramissionReturnsTrue()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ServerPingResponse, default))
                .ReturnsAsync(true);

            var sut = new ServerController(
                clientNotificationServiceMock.Object, Mock.Of<ILogger<ServerController>>()
                );

            var result = await sut.PingAsync(default);

            Assert.IsType<OkResult>(result);

            var resultAsOkResult = (OkResult)result;
            Assert.Equal(StatusCodes.Status200OK, resultAsOkResult.StatusCode);
        }

        [Fact]
        public async Task PingAsyncReturnsStatus400WhenTramissionReturnsFalse()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ServerPingResponse, default))
                .ReturnsAsync(false);

            var sut = new ServerController(
                clientNotificationServiceMock.Object, Mock.Of<ILogger<ServerController>>()
                );

            var result = await sut.PingAsync(default);

            Assert.IsType<ObjectResult>(result);

            var resultAsObjectResult = (ObjectResult)result;
            Assert.Equal(StatusCodes.Status400BadRequest, resultAsObjectResult.StatusCode);
        }

        [Fact]
        public async Task PingAsyncReturnsStatus408WhenTramissionGetsCancelled()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ServerPingResponse, default))
                .ThrowsAsync(new OperationCanceledException());

            var sut = new ServerController(
                clientNotificationServiceMock.Object, Mock.Of<ILogger<ServerController>>()
                );

            var result = await sut.PingAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status408RequestTimeout, resultAsStatusCodeResult.StatusCode);
        }

        [Fact]
        public async Task PingAsyncReturnsStatus500WhenTramissionThrows()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(Messages.ServerPingResponse, default))
                .ThrowsAsync(new Exception("Unexpected exception."));

            var sut = new ServerController(
                clientNotificationServiceMock.Object, Mock.Of<ILogger<ServerController>>()
                );

            var result = await sut.PingAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status500InternalServerError, resultAsStatusCodeResult.StatusCode);
        }
    }
}
