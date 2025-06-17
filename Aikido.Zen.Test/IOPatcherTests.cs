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

        private void RunAndVerifyAttackFlag(string[] paths, MethodInfo methodInfo, bool expectAttack, bool expectBlocked)
        {
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false;

            if (expectBlocked)
            {
                Assert.Throws<AikidoException>(
                    () => IOPatcher.OnFileOperation(paths, methodInfo, contextToPass),
                    "Expected AikidoException for blocked path traversal."
                );
            }
            else
            {
                var result = IOPatcher.OnFileOperation(paths, methodInfo, contextToPass);
                Assert.That(result, Is.True, "OnFileOperation should return true when not blocking.");
            }

            Assert.That(contextToPass.AttackDetected, Is.EqualTo(expectAttack), $"Context AttackDetected flag should be {expectAttack}.");
        }

        [Test]
        public void OnFileOperation_WithNullContext_ReturnsTrue()
        {
            var paths = new[] { "safe/path/file.txt" };
            var result = IOPatcher.OnFileOperation(paths, _methodInfo, null);
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnFileOperation_WithSafePathAndNoMatchingUserInput_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var paths = new[] { "data/repository/somefile.txt" };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.id", "123" } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            var paths = new[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ReturnsTrueWhenNotBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            var paths = new[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_PathContainsSafeUserInput_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var safeInput = "safe_file.txt";
            var pathArgument = $"/var/data/files/{safeInput}";
            var paths = new[] { pathArgument };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.filename", safeInput } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_MultiplePathsOneUnsafe_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) });
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            var paths = new[] { safeSource, unsafeDest };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };
            RunAndVerifyAttackFlag(paths, copyMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_MultiplePathsOneUnsafe_ReturnsTrueWhenNotBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) });
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            var paths = new[] { safeSource, unsafeDest };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };
            RunAndVerifyAttackFlag(paths, copyMethodInfo, expectAttack: true, expectBlocked: false);
        }

        [TestCase("/etc/shadow")]
        [TestCase("c:/windows/system32/cmd.exe")]
        [TestCase("C:\\Windows\\System32\\cmd.exe")]
        [TestCase("%WINDIR%\\system32\\notepad.exe")]
        public void OnFileOperation_AbsoluteInput_ThrowsExceptionWhenBlocking(string absoluteInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var paths = new[] { absoluteInput };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [TestCase("/etc/shadow")]
        [TestCase("c:/windows/system32/cmd.exe")]
        [TestCase("C:\\Windows\\System32\\cmd.exe")]
        [TestCase("%WINDIR%\\system32\\notepad.exe")]
        public void OnFileOperation_AbsoluteInput_ReturnsTrueWhenNotBlocking(string absoluteInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var paths = new[] { absoluteInput };
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };
            RunAndVerifyAttackFlag(paths, _methodInfo, expectAttack: true, expectBlocked: false);
        }
    }
}
