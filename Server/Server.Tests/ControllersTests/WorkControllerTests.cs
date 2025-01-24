using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Server.Auxiliary;
using Server.ClientNotifications;
using Server.Controllers;

namespace Server.Tests.ControllersTests
{
    public class WorkControllerTests
    {
        [Fact]
        public void ConstructorThrowsArgNullWhenClientNotificationServiceIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorkController(
                    null,
                    Mock.Of<IGuidProvider>(),
                    Mock.Of<IWorker>(),
                    Mock.Of<ILogger<WorkController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenGuidProviderIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorkController(
                    Mock.Of<IClientNotificationService>(),
                    null,
                    Mock.Of<IWorker>(),
                    Mock.Of<ILogger<WorkController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenWorkerIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorkController(
                    Mock.Of<IClientNotificationService>(),
                    Mock.Of<IGuidProvider>(),
                    null,
                    Mock.Of<ILogger<WorkController>>()
                    )
                );
        }

        [Fact]
        public void ConstructorThrowsArgNullWhenLoggerIsAbsent()
        {
            Assert.Throws<ArgumentNullException>(
                () => new WorkController(
                    Mock.Of<IClientNotificationService>(),
                    Mock.Of<IGuidProvider>(),
                    Mock.Of<IWorker>(),
                    null
                    )
                );
        }

        /// <summary>
        /// The designed workflow is:
        ///     Generate an ID to represent the unit of work.
        ///     Trasmit the notificaiton indicate the work has started, including the ID of work.
        ///     Execute the work.
        ///     Trasmit the notificaiton indicate the work has completed, including the ID of work.
        /// </summary>
        [Fact]
        public async Task StartAsyncExecutesTheDesignedWorkflowInSequenceAndReturnsStatus200()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            var sequence = new MockSequence();

            var expectedGuid = new Guid("f29f8268-38b2-48a5-8a66-963d66dd60f4");

            guidProviderMock.InSequence(sequence)
                .Setup(gpm => gpm.GetGuid())
                .Returns(expectedGuid);

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(
                        s => s.Contains(Messages.WorkStartedNotification) && s.Contains(expectedGuid.ToString())
                        ),
                    default))
                .ReturnsAsync(true);

            workerMock.InSequence(sequence)
                .Setup(wm => wm.PretendToWorkAsync(default))
                .Returns(Task.CompletedTask);

            clientNotificationServiceMock.InSequence(sequence)
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(
                        s => s.Contains(Messages.WorkCompletedNotification) && s.Contains(expectedGuid.ToString())
                        ),
                    default))
                .ReturnsAsync(true);

            var result =  await sut.StartAsync(default);

            guidProviderMock
                .Verify(gpm => gpm.GetGuid(), Times.Once);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(
                        s => s.Contains(Messages.WorkStartedNotification) && s.Contains(expectedGuid.ToString())
                        ),
                    default),
                    Times.Once);

            workerMock
                .Verify(wm => wm.PretendToWorkAsync(default), Times.Once);

            clientNotificationServiceMock
                .Verify(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(
                        s => s.Contains(Messages.WorkCompletedNotification) && s.Contains(expectedGuid.ToString())
                        ),
                    default),
                    Times.Once);

            Assert.IsType<OkResult>(result);

            var resultAsOkResult = (OkResult)result;
            Assert.Equal(StatusCodes.Status200OK, resultAsOkResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus400WhenWorkStartNotificationFails()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)),default))
                .ReturnsAsync(false);

            var result = await sut.StartAsync(default);

            Assert.IsType<ObjectResult>(result);

            var resultAsObjectResult = (ObjectResult)result;
            Assert.Equal(StatusCodes.Status400BadRequest, resultAsObjectResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus400WhenWorkCompletedNotificationFails()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ReturnsAsync(true);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkCompletedNotification)), default))
                .ReturnsAsync(false);

            var result = await sut.StartAsync(default);

            Assert.IsType<ObjectResult>(result);

            var resultAsObjectResult = (ObjectResult)result;
            Assert.Equal(StatusCodes.Status400BadRequest, resultAsObjectResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus500WhenIDGenerationThrows()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            guidProviderMock.Setup(gpm => gpm.GetGuid()).Throws(new Exception("Bad things happened."));

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status500InternalServerError, resultAsStatusCodeResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus500WhenWorkStartNotificationTransmissionThrows()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status500InternalServerError, resultAsStatusCodeResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus500WhenWorkerThrows()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ReturnsAsync(true);

            workerMock
                .Setup(wm => wm.PretendToWorkAsync(default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status500InternalServerError, resultAsStatusCodeResult.StatusCode);
        }


        [Fact]
        public async Task StartAsyncReturnsStatus500WhenWorkCompletedNotificationTransmissionThrows()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ReturnsAsync(true);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkCompletedNotification)), default))
                .ThrowsAsync(new Exception("Bad things happened."));

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status500InternalServerError, resultAsStatusCodeResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus408WhenWorkStartNotificationTransmissionGetsCancelled()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ThrowsAsync(new OperationCanceledException());

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status408RequestTimeout, resultAsStatusCodeResult.StatusCode);
        }

        [Fact]
        public async Task StartAsyncReturnsStatus408WhenWorkerGetsWorkCancelled()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ReturnsAsync(true);

            workerMock
                .Setup(wm => wm.PretendToWorkAsync(default))
                .ThrowsAsync(new OperationCanceledException());

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status408RequestTimeout, resultAsStatusCodeResult.StatusCode);
        }


        [Fact]
        public async Task StartAsyncReturnsStatus408WhenWorkCompletedNotificationTransmissionGetsCancelled()
        {
            var clientNotificationServiceMock = new Mock<IClientNotificationService>();
            var guidProviderMock = new Mock<IGuidProvider>();
            var workerMock = new Mock<IWorker>();

            var sut = new WorkController(
                clientNotificationServiceMock.Object,
                guidProviderMock.Object,
                workerMock.Object,
                Mock.Of<ILogger<WorkController>>()
                );

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkStartedNotification)), default))
                .ReturnsAsync(true);

            clientNotificationServiceMock
                .Setup(cnsm => cnsm.TransmitAsync(
                    It.Is<string>(s => s.Contains(Messages.WorkCompletedNotification)), default))
                .ThrowsAsync(new OperationCanceledException());

            var result = await sut.StartAsync(default);

            Assert.IsType<StatusCodeResult>(result);

            var resultAsStatusCodeResult = (StatusCodeResult)result;
            Assert.Equal(StatusCodes.Status408RequestTimeout, resultAsStatusCodeResult.StatusCode);
        }
    }
}
