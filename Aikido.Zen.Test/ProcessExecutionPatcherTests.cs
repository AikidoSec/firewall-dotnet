using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Patches;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Tests for the ProcessExecutionPatcher class.
    /// </summary>
    public class ProcessExecutionPatcherTests
    {
        private ProcessStartInfo _startInfo;
        private Context _context;
        private MethodInfo _methodInfo;

        [SetUp]
        public void Setup()
        {
            _startInfo = new ProcessStartInfo();
            _context = new Context();
            _methodInfo = typeof(Process).GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");
            // setup the agent, because when not running in drymode, SqlClientPatcher will trigger an attack event
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            var reportingMock = new Mock<IReportingAPIClient>();
            reportingMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()))
                    .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeMock = new Mock<IRuntimeAPIClient>();
            runtimeMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var zenApiMock = new ZenApi(reportingMock.Object, runtimeMock.Object);

            Agent.NewInstance(zenApiMock);
        }

        [Test]
        public void OnProcessStart_WithNullContext_ReturnsTrue()
        {
            // Arrange
            var args = new object[] { };

            // Act
            var result = ProcessExecutionPatcher.OnProcessStart(args, _methodInfo, new Process { StartInfo = _startInfo }, null);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithSafeCommand_ReturnsTrue()
        {
            // Arrange
            _startInfo.FileName = "safeCommand";
            _startInfo.Arguments = "--safe";
            var args = new object[] { };

            // Act
            var result = ProcessExecutionPatcher.OnProcessStart(args, _methodInfo, new Process { StartInfo = _startInfo }, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnProcessStart_WithShellInjection_ThrowsException()
        {
            // Arrange
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "body.command", "$(echo)" }
            };
            _startInfo.FileName = "sh";
            _startInfo.Arguments = "-c \"$(echo)\"";
            var args = new object[] { };

            // Act & Assert
            var ex = Assert.Throws<AikidoException>(() =>
                ProcessExecutionPatcher.OnProcessStart(args, _methodInfo, new Process { StartInfo = _startInfo }, _context)
            );
            Assert.That(ex.Message, Does.Contain("Shell injection detected"));
        }

        [Test]
        public void OnProcessStart_WithShellInjectionInDryMode_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "false");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "body.command", "maliciousCommand" }
            };
            _startInfo.FileName = "maliciousCommand";
            _startInfo.Arguments = "--inject";
            var args = new object[] { };

            // Act
            var result = ProcessExecutionPatcher.OnProcessStart(args, _methodInfo, new Process { StartInfo = _startInfo }, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", null);
        }
    }
}
