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

        public class TestMethods
        {
            public void MethodWithPath(string path) { }
            public void MethodWithPathArray(string[] paths) { }
            public void MethodWithNonPathArg(string someArg) { }
            public void MethodWithMixedArgs(string path, string someArg) { }
            public void MethodWithRefParam(string name, ref string path) { }
            public void MethodWithOutParam(string name, out string path) { path = "../secrets.txt"; }
            public void MethodWithParams(string name, params string[] paths) { }
            public void MethodWithOptional(string name, string path = "default.txt") { }
        }

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

        [Test]
        public void HandlesParamsCorrectly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../secrets.txt";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.path", unsafeInput } };

            // Test params array
            var methodWithParams = typeof(TestMethods).GetMethod("MethodWithParams");
            var methodWithParamsArgs = new object[] { "safe.txt", unsafeInput };
            RunAndVerifyAttackFlag(methodWithParamsArgs, methodWithParams, expectAttack: true, expectBlocked: true);

            // Test optional parameter
            var methodWithOptional = typeof(TestMethods).GetMethod("MethodWithOptional");
            var methodWithOptionalArgs = new object[] { "safe.txt", unsafeInput };
            RunAndVerifyAttackFlag(methodWithOptionalArgs, methodWithOptional, expectAttack: true, expectBlocked: true);

            // Test ref parameter
            var methodWithRefParam = typeof(TestMethods).GetMethod("MethodWithRefParam");
            var methodWithRefParamArgs = new object[] { "safe.txt", unsafeInput };
            RunAndVerifyAttackFlag(methodWithRefParamArgs, methodWithRefParam, expectAttack: true, expectBlocked: true);

            // Test out parameter
            var methodWithOutParam = typeof(TestMethods).GetMethod("MethodWithOutParam");
            var methodWithOutParamArgs = new object[] { "safe.txt", unsafeInput };
            RunAndVerifyAttackFlag(methodWithOutParamArgs, methodWithOutParam, expectAttack: true, expectBlocked: true);

            // Test array parameter
            var methodWithPathArray = typeof(TestMethods).GetMethod("MethodWithPathArray");
            var methodWithPathArrayArgs = new object[] { new string[] { "safe.txt", unsafeInput } };
            RunAndVerifyAttackFlag(methodWithPathArrayArgs, methodWithPathArray, expectAttack: true, expectBlocked: true);

            // Test mixed parameters
            var methodWithMixedArgs = typeof(TestMethods).GetMethod("MethodWithMixedArgs");
            var methodWithMixedArgsArgs = new object[] { unsafeInput, "safe.txt" };
            RunAndVerifyAttackFlag(methodWithMixedArgsArgs, methodWithMixedArgs, expectAttack: true, expectBlocked: true);
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
