using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Sinks;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Tests for the ProcessExecutionSink class.
    /// </summary>
    public class ProcessExecutionSinkTests
    {
        private ProcessStartInfo _startInfo = null!;
        private Context _context = null!;
        private MethodInfo _methodInfo = null!;
        private Context? _activeContext;

        [SetUp]
        public void Setup()
        {
            _startInfo = new ProcessStartInfo();
            _context = new Context();
            _methodInfo = typeof(Process).GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
            // setup the agent, because when not running in drymode, SqlClientSink will trigger an attack event
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            var reportingMock = new Mock<IReportingAPIClient>();
            reportingMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeMock = new Mock<IRuntimeAPIClient>();
            runtimeMock
                .Setup(r => r.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var zenApiMock = new ZenApi(reportingMock.Object, runtimeMock.Object);

            Agent.NewInstance(zenApiMock);
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [Test]
        public void OnProcessStart_WithNullContext_ReturnsTrue()
        {
            // Arrange

            // Act
            var result = OnProcessStart(new Process { StartInfo = _startInfo }, null);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithNullProcess_ReturnsTrue()
        {
            var result = OnProcessStart(null, _context);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithBypassedContext_ReturnsTrue()
        {
            _context.Bypassed = true;
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.command", "$(echo)" }
            };
            _startInfo.FileName = "sh";
            _startInfo.Arguments = "-c \"$(echo)\"";

            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnProcessStart_WithSafeCommand_ReturnsTrue()
        {
            // Arrange
            _startInfo.FileName = "safeCommand";
            _startInfo.Arguments = "--safe";

            // Act
            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithShellInjection_ThrowsException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "body.command", "$(echo)" }
            };
            _startInfo.FileName = "sh";
            _startInfo.Arguments = "-c \"$(echo)\"";

            // Act & Assert
            var ex = Assert.Throws<AikidoException>(() =>
                OnProcessStart(new Process { StartInfo = _startInfo }, _context)
            );
            Assert.That(ex.Message, Does.Contain("Zen has blocked a shell injection"));
        }

        [Test]
        public void OnProcessStart_WithShellInjectionInDryMode_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "body.command", "maliciousCommand" }
            };
            _startInfo.FileName = "maliciousCommand";
            _startInfo.Arguments = "--inject";

            // Act
            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithForceProtectionOffRoute_ReturnsTrueWithoutMarkingAttack()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.Method = "POST";
            _context.Route = "/api/execute";
            _context.Path = "/api/execute";
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.command", "$(echo)" }
            };
            _startInfo.FileName = "sh";
            _startInfo.Arguments = "-c \"$(echo)\"";

            Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "POST",
                    Route = "/api/execute",
                    ForceProtectionOff = true
                }
            });

            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnProcessStart_WithMissingUserInputCollection_ReturnsTrue()
        {
#pragma warning disable CS8625
            _context.ParsedUserInput = null;
#pragma warning restore CS8625
            _startInfo.FileName = "sh";
            _startInfo.Arguments = "-c \"$(echo)\"";

            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
        }

        private bool OnProcessStart(Process? process, Context? context)
        {
            _activeContext = context;
            return ProcessExecutionSink.OnProcessStartInstance(process!, _methodInfo);
        }
    }
}
