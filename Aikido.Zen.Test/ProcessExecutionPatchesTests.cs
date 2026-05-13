using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class ProcessExecutionPatchesTests
    {
        private Context _context = null!;
        private Agent _agent = null!;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeApiMock = new Mock<IRuntimeAPIClient>();

            _agent = Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            Patcher.PatchSinks(() => _context);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
        }

        [Test]
        public void PatchMethod_ForwardsProcessArgumentsToSink()
        {
            using (var process = new Process { StartInfo = new ProcessStartInfo("echo", "safe") })
            {
                Assert.That(ProcessExecutionPatches.OnProcessStartInstance(
                    process,
                    GetMethod(typeof(Process), nameof(Process.Start))), Is.True);
            }

            Assert.That(ProcessExecutionPatches.OnProcessStartInstance(
                null!,
                GetMethod(typeof(Process), nameof(Process.Start))), Is.True);
        }

        private static MethodInfo GetMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }
    }
}
