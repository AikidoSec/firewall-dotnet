using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Patches;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class IOPatcherTests
    {
        private Context _realContext;
        private Mock<Context> _mockContext; // Mock to allow verification if needed, CallBase ensures real logic runs
        private MethodInfo _methodInfo;
        private Mock<IReportingAPIClient> _reportingMock;
        private Mock<IRuntimeAPIClient> _runtimeMock;
        private Mock<ZenApi> _zenApiMock;

        [SetUp]
        public void Setup()
        {
            _realContext = new Context();
            // Use CallBase = true so methods like setting ParsedUserInput work,
            // and AttackDetected gets set by the underlying PathTraversalHelper call.
            _mockContext = new Mock<Context>() { CallBase = true };
            _methodInfo = typeof(File).GetMethod("ReadAllBytes", new[] { typeof(string) });

            _reportingMock = new Mock<IReportingAPIClient>();
            _runtimeMock = new Mock<IRuntimeAPIClient>();
            _zenApiMock = new Mock<ZenApi>(_reportingMock.Object, _runtimeMock.Object);

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);

            Agent.NewInstance(_zenApiMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
        }

        // Helper to run the operation and verify AttackDetected flag
        private void RunAndVerifyAttackFlag(object[] args, MethodInfo methodInfo, bool expectAttack, bool expectBlocked)
        {
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false; // Reset flag before test

            if (expectBlocked)
            {
                var assemblyName = methodInfo.DeclaringType.Assembly.FullName?.Split(',')[0].Trim();
                Assert.Throws<AikidoException>(
                    () => IOPatcher.OnFileOperation(args, methodInfo, contextToPass),
                    "Expected AikidoException for blocked path traversal."
                );
            }
            else
            {
                var result = IOPatcher.OnFileOperation(args, methodInfo, contextToPass);
                Assert.That(result, Is.True, "OnFileOperation should return true when not blocking.");
            }

            // Verify AttackDetected flag on the context object passed to the patcher
            if (expectAttack)
            {
                Assert.That(contextToPass.AttackDetected, Is.True, "Context AttackDetected flag should be true when attack is expected.");
            }
            else
            {
                Assert.That(contextToPass.AttackDetected, Is.False, "Context AttackDetected flag should be false when attack is not expected.");
            }
        }

        [Test]
        public void OnFileOperation_WithNullContext_ReturnsTrue()
        {
            // Arrange
            var args = new object[] { "safe/path/file.txt" };

            // Act
            var result = IOPatcher.OnFileOperation(args, _methodInfo, null);

            // Assert
            Assert.That(result, Is.True);
            // Cannot check AttackDetected flag on null context
        }

        [Test]
        public void OnFileOperation_WithSafePathAndNoMatchingUserInput_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var args = new object[] { "data/repository/somefile.txt" };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.id", "123" } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ThrowsExceptionWhenBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            var args = new object[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ReturnsTrueWhenNotBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            var args = new object[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_PathContainsSafeUserInput_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var safeInput = "safe_file.txt";
            var pathArgument = $"/var/data/files/{safeInput}";
            var args = new object[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.filename", safeInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_MultipleArgsOneUnsafe_ThrowsExceptionWhenBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) });
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            var args = new object[] { safeSource, unsafeDest, true };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, copyMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_MultipleArgsOneUnsafe_ReturnsTrueWhenNotBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) });
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            var args = new object[] { safeSource, unsafeDest, true };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, copyMethodInfo, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_NonStringArgument_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var openMethodInfo = typeof(File).GetMethod("Open", new[] { typeof(string), typeof(FileMode) });
            var pathArgument = "config/settings.xml";
            var args = new object[] { pathArgument, FileMode.Open };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "unrelated", "value" } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, openMethodInfo, expectAttack: false, expectBlocked: false);
        }

        [TestCase("/etc/shadow")]
        [TestCase("c:/windows/system32/cmd.exe")]
        [TestCase("C:\\Windows\\System32\\cmd.exe")]
        [TestCase("%WINDIR%\\system32\\notepad.exe")]
        public void OnFileOperation_AbsoluteInput_ThrowsExceptionWhenBlocking(string absoluteInput)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var pathArgument = absoluteInput;
            var args = new object[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [TestCase("/etc/shadow")]
        [TestCase("c:/windows/system32/cmd.exe")]
        [TestCase("C:\\Windows\\System32\\cmd.exe")]
        [TestCase("%WINDIR%\\system32\\notepad.exe")]
        public void OnFileOperation_AbsoluteInput_ReturnsTrueWhenNotBlocking(string absoluteInput)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var pathArgument = absoluteInput;
            var args = new object[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };

            // Act & Assert
            RunAndVerifyAttackFlag(args, _methodInfo, expectAttack: true, expectBlocked: false);
        }
    }
}
