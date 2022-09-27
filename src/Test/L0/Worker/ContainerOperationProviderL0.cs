using GitHub.Runner.Worker;
using GitHub.Runner.Worker.Container;
using Xunit;
using Moq;
using GitHub.Runner.Worker.Container.ContainerHooks;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GitHub.DistributedTask.WebApi;
using System;

namespace GitHub.Runner.Common.Tests.Worker
{

    public sealed class ContainerOperationProviderL0
    {

        private TestHostContext _hc;
        private Mock<IExecutionContext> _ec;
        private Mock<IDockerCommandManager> _dockerManager;
        private Mock<IContainerHookManager> _containerHookManager;
        private ContainerOperationProvider containerOperationProvider;
        private Mock<IJobServerQueue> serverQueue;
        private Mock<IPagingLogger> pagingLogger;
        private List<string> healthyDockerStatus = new List<string> { "healthy" };
        private List<string> unhealthyDockerStatus = new List<string> { "unhealthy" };
        private List<string> emptyDockerStatus = new List<string> { "" };
        private List<string> dockerLogs = new List<string> { "log1", "log2", "log3" };
        string healthCheck = "--format=\"{{if .Config.Healthcheck}}{{print .State.Health.Status}}{{end}}\"";

        List<ContainerInfo> containers = new List<ContainerInfo>();

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_UnhealthyServiceContainer_AssertFailedTask()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), healthCheck)).Returns(Task.FromResult(unhealthyDockerStatus));

            //Act
            try
            {
                await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);
            }
            catch (InvalidOperationException)
            {

                //Assert
                Assert.Equal(TaskResult.Failed, _ec.Object.Result ?? TaskResult.Failed);
                _dockerManager.Verify(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_UnhealthyServiceContainer_AssertExceptionThrown()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), healthCheck)).Returns(Task.FromResult(unhealthyDockerStatus));

            //Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers));
            _dockerManager.Verify(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_HealthyServiceContainer_AssertSucceededTask()
        {
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), healthCheck)).Returns(Task.FromResult(healthyDockerStatus));

            //Act
            await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);

            //Assert
            Assert.Equal(TaskResult.Succeeded, _ec.Object.Result ?? TaskResult.Succeeded);
            _dockerManager.Verify(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>()), Times.Once());

        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_ServiceContainerWithoutHealthcheckAndWithOkExitStatus_AssertSucceededTask()
        {

            string exitCode = "--format=\"{{print .State.ExitCode}}\"";
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), healthCheck)).Returns(Task.FromResult(emptyDockerStatus));
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), exitCode)).Returns(Task.FromResult(new List<string> { "0" }));

            //Act
            await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);

            //Assert
            Assert.Equal(TaskResult.Succeeded, _ec.Object.Result ?? TaskResult.Succeeded);
            _dockerManager.Verify(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));

        }


        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public async void RunServiceContainersHealthcheck_ServiceContainerWithoutHealthcheckAndWithErrorExitStatus_AssertSucceededTask()
        {

            string exitCode = "--format=\"{{print .State.ExitCode}}\"";
            //Arrange
            Setup();
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), healthCheck)).Returns(Task.FromResult(emptyDockerStatus));
            _dockerManager.Setup(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), exitCode)).Returns(Task.FromResult(new List<string> { "127" }));

            //Act
            try
            {
                await containerOperationProvider.RunContainersHealthcheck(_ec.Object, containers);

            }
            catch (InvalidOperationException)
            {
                //Assert
                Assert.Equal(TaskResult.Failed, _ec.Object.Result ?? TaskResult.Failed);
                _dockerManager.Verify(x => x.DockerInspect(_ec.Object, It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            }

        }

        private void Setup([CallerMemberName] string testName = "")
        {
            containers.Add(new ContainerInfo() { ContainerImage = "ubuntu:16.04" });
            _hc = new TestHostContext(this, testName);
            _ec = new Mock<IExecutionContext>();
            serverQueue = new Mock<IJobServerQueue>();
            pagingLogger = new Mock<IPagingLogger>();

            _dockerManager = new Mock<IDockerCommandManager>();
            _containerHookManager = new Mock<IContainerHookManager>();
            containerOperationProvider = new ContainerOperationProvider();

            _hc.SetSingleton<IDockerCommandManager>(_dockerManager.Object);
            _hc.SetSingleton<IJobServerQueue>(serverQueue.Object);
            _hc.SetSingleton<IPagingLogger>(pagingLogger.Object);

            _hc.SetSingleton<IDockerCommandManager>(_dockerManager.Object);
            _hc.SetSingleton<IContainerHookManager>(_containerHookManager.Object);

            _ec.Setup(x => x.Global).Returns(new GlobalContext());

            containerOperationProvider.Initialize(_hc);
        }
    }
}