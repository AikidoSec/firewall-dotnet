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

        [TestCase("sh", "-c \"echo ok;id\"", "echo ok;id")]
        [TestCase("sh", "-c \"echo ok && id\"", "echo ok && id")]
        [TestCase("sh", "-c \"echo ok\nid\"", "echo ok\nid")]
        [TestCase("/bin/bash", "-c \"id\"", "id")]
        [TestCase("sh", "-c \"curl -s https://example.com/x -o /tmp/x\"", "curl -s https://example.com/x -o /tmp/x")]
        [TestCase("sh", "-c \"echo a\";id;\"\"", "a\";id;\"")]
        [TestCase("pwsh", "-c \"Get-Date; whoami\"", "Get-Date; whoami")]
        [TestCase("pwsh", "-Com \"Get-Date; whoami\"", "Get-Date; whoami")]
        [TestCase("powershell.exe", "/co \"Get-Date; whoami\"", "Get-Date; whoami")]
        [TestCase("pwsh", "-ExecutionPolicy Bypass -Command \"Get-Date; whoami\"", "Get-Date; whoami")]
        [TestCase("bash", "-o errexit -c \"echo FIRST; echo SECOND\"", "echo FIRST; echo SECOND")]
        [TestCase("cmd.exe", "/c \"echo ok & whoami\"", "echo ok & whoami")]
        [TestCase("cmd.exe", "/d /s /c \"echo ok & whoami\"", "echo ok & whoami")]
        public void OnProcessStart_WithShellInjectionInArguments_ThrowsException(string fileName, string arguments, string userInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "query.command", userInput }
            };
            _startInfo.FileName = fileName;
            _startInfo.Arguments = arguments;

            var ex = Assert.Throws<AikidoException>(() =>
                OnProcessStart(new Process { StartInfo = _startInfo }, _context)
            );
            Assert.That(ex.Message, Does.Contain("Zen has blocked a shell injection"));
        }

        [TestCase("sh", new[] { "-c", "echo ok;id" }, "echo ok;id")]
        [TestCase("pwsh", new[] { "-CoM", "Get-Date; whoami" }, "Get-Date; whoami")]
        [TestCase("pwsh", new[] { "--command", "Get-Date; whoami" }, "Get-Date; whoami")]
        [TestCase("pwsh", new[] { " -Com ", "Get-Date; whoami" }, "Get-Date; whoami")]
        [TestCase("cmd.exe", new[] { "/c", "echo ok & whoami" }, "echo ok & whoami")]
        [TestCase("deploy.bat", new[] { "\"&whoami" }, "\"&whoami")]
        public void OnProcessStart_WithShellInjectionInArgumentList_ThrowsException(string fileName, string[] arguments, string userInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "query.command", userInput }
            };
            _startInfo.FileName = fileName;
            foreach (var argument in arguments)
            {
                _startInfo.ArgumentList.Add(argument);
            }

            var ex = Assert.Throws<AikidoException>(() =>
                OnProcessStart(new Process { StartInfo = _startInfo }, _context)
            );
            Assert.That(ex.Message, Does.Contain("Zen has blocked a shell injection"));
        }

        [TestCase("bash", "-c \"grep 'foo;id' /var/log/app.log\"", "foo;id")]
        [TestCase("sh", "-c \"echo 'hello world'\"", "hello world")]
        [TestCase("git", "commit -m \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("git", "commit -m \"Tom & Jerry\" -- sh", "Tom & Jerry")]
        [TestCase("ls", "\"whatever;\"", "whatever;")]
        [TestCase("cmd.exe", "/c echo \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("cmd.exe", "/c \"echo \"Tom & Jerry\"\"", "Tom & Jerry")]
        [TestCase("cmd.exe", "/c echo \"a\" /c\"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("bash", "deploy.sh \"two words\"", "two words")]
        [TestCase("sh", "-- script.sh -c \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("pwsh", "-File script.ps1 \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("pwsh", "-File script.ps1 -Com \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("pwsh", "-f script.ps1 -Command \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("pwsh", "-cwa \"Write-Output $args[0]\" -Com \"Tom & Jerry\"", "Tom & Jerry")]
        [TestCase("deploy.bat", "\"Tom & Jerry\"", "Tom & Jerry")]
        public void OnProcessStart_WithSafeUserInput_ReturnsTrue(string fileName, string arguments, string userInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "query.input", userInput }
            };
            _startInfo.FileName = fileName;
            _startInfo.Arguments = arguments;

            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [TestCase("git", new[] { "commit", "-m", "Tom & Jerry; $(id)" }, "Tom & Jerry; $(id)")]
        [TestCase("cmd.exe", new[] { "/c", "echo", "Tom & Jerry" }, "Tom & Jerry")]
        [TestCase("sh", new[] { "-c", "echo \"$1\"", "sh", "Tom & Jerry" }, "Tom & Jerry")]
        [TestCase("pwsh", new[] { "-File", "script.ps1", "-Com", "Tom & Jerry" }, "Tom & Jerry")]
        [TestCase("pwsh", new[] { "-CommandWithArgs", "Write-Output $args[0]", "-Com", "Tom & Jerry" }, "Tom & Jerry")]
        [TestCase("deploy.bat", new[] { "Tom & Jerry" }, "Tom & Jerry")]
        public void OnProcessStart_WithSafeUserInputInArgumentList_ReturnsTrue(string fileName, string[] arguments, string userInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string> {
                { "query.message", userInput }
            };
            _startInfo.FileName = fileName;
            foreach (var argument in arguments)
            {
                _startInfo.ArgumentList.Add(argument);
            }

            var result = OnProcessStart(new Process { StartInfo = _startInfo }, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
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
