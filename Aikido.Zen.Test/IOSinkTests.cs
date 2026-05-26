using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Sinks;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class IOSinkTests
    {
        private Context _realContext;
        private Mock<Context> _mockContext; // Mock to allow verification if needed, CallBase ensures real logic runs
        private MethodInfo _methodInfo;
        private Mock<IReportingAPIClient> _reportingMock;
        private Mock<IRuntimeAPIClient> _runtimeMock;
        private Mock<ZenApi> _zenApiMock;
        private Context? _activeContext;

        [SetUp]
        public void Setup()
        {
            _realContext = new Context();
            _mockContext = new Mock<Context>() { CallBase = true };
            _methodInfo = typeof(File).GetMethod("ReadAllBytes", new[] { typeof(string) })!;

            _reportingMock = new Mock<IReportingAPIClient>();
            _runtimeMock = new Mock<IRuntimeAPIClient>();
            _zenApiMock = new Mock<ZenApi>(_reportingMock.Object, _runtimeMock.Object);

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);

            Agent.NewInstance(_zenApiMock.Object);
            _activeContext = _mockContext.Object;
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
        }

        private void RunAndVerifyAttackFlag(string path, MethodInfo methodInfo, bool expectAttack, bool expectBlocked)
        {
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false;

            if (expectBlocked)
            {
                Assert.Throws<AikidoException>(
                    () => OnFileOperation(path, methodInfo, contextToPass),
                    "Expected AikidoException for blocked path traversal."
                );
            }
            else
            {
                var result = OnFileOperation(path, methodInfo, contextToPass);
                Assert.That(result, Is.True, "OnFileOperation should return true when not blocking.");
            }

            Assert.That(contextToPass.AttackDetected, Is.EqualTo(expectAttack), $"Context AttackDetected flag should be {expectAttack}.");
        }

        [Test]
        public void OnFileOperation_WithNullContext_ReturnsTrue()
        {
            var result = OnFileOperation("safe/path/file.txt", _methodInfo, null);
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnFileOperation_WithBypassedContext_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var context = new Context
            {
                Bypassed = true,
                ParsedUserInput = new Dictionary<string, string> { { "query.path", "../secret.txt" } }
            };

            var result = OnFileOperation("/var/www/data/../secret.txt", _methodInfo, context);

            Assert.That(result, Is.True);
            Assert.That(context.AttackDetected, Is.False);
        }

        [Test]
        public void OnFileOperation_WithSafePathAndNoMatchingUserInput_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.id", "123" } };
            RunAndVerifyAttackFlag("data/repository/somefile.txt", _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };
            RunAndVerifyAttackFlag(pathArgument, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_WithUserInputPathTraversal_ReturnsTrueWhenNotBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var unsafeInput = "../etc/passwd";
            var pathArgument = $"/var/www/data/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url.filename", unsafeInput } };
            RunAndVerifyAttackFlag(pathArgument, _methodInfo, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_PathContainsSafeUserInput_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var safeInput = "safe_file.txt";
            var pathArgument = $"/var/data/files/{safeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.filename", safeInput } };
            RunAndVerifyAttackFlag(pathArgument, _methodInfo, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_MultiplePathsOneUnsafe_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) })!;
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false;

            Assert.Throws<AikidoException>(
                () => OnFileOperationTwoPaths(safeSource, unsafeDest, copyMethodInfo, contextToPass),
                "Expected AikidoException for blocked path traversal.");
            Assert.That(contextToPass.AttackDetected, Is.True);
        }

        [Test]
        public void OnFileOperation_MultiplePathsOneUnsafe_ReturnsTrueWhenNotBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var copyMethodInfo = typeof(File).GetMethod("Copy", new[] { typeof(string), typeof(string), typeof(bool) })!;
            var unsafeInput = "../../secrets.txt";
            var safeSource = "/app/uploads/image.jpg";
            var unsafeDest = $"/app/static/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "form.dest_path", unsafeInput } };
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false;

            Assert.That(OnFileOperationTwoPaths(safeSource, unsafeDest, copyMethodInfo, contextToPass), Is.True);
            Assert.That(contextToPass.AttackDetected, Is.True);
        }

        [TestCase(true, Description = "Unsafe source")]
        [TestCase(false, Description = "Unsafe destination")]
        public void OnFileOperation_FileMoveWithUnsafePath_ThrowsExceptionWhenBlocking(bool unsafeSource)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var moveMethodInfo = typeof(File).GetMethod("Move", new[] { typeof(string), typeof(string) })!;
            var unsafeInput = "../../secrets.txt";
            var safePath = "/app/uploads/image.jpg";
            var unsafePath = $"/app/static/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "body.file.matches", unsafeInput } };
            var contextToPass = _mockContext.Object;
            contextToPass.ParsedUserInput = _realContext.ParsedUserInput;
            contextToPass.AttackDetected = false;

            Assert.Throws<AikidoException>(
                () => OnFileOperationTwoPaths(
                    unsafeSource ? unsafePath : safePath,
                    unsafeSource ? safePath : unsafePath,
                    moveMethodInfo,
                    contextToPass),
                "Expected AikidoException for blocked path traversal.");
            Assert.That(contextToPass.AttackDetected, Is.True);
        }

        [Test]
        public void OnFileOperation_GetFullPathWithTraversal_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var getFullPathMethodInfo = typeof(Path).GetMethod("GetFullPath", new[] { typeof(string) })!;
            var unsafeInput = "../secrets/key.txt";
            var pathArgument = Path.Combine("wwwroot/blogs", unsafeInput);
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.path", unsafeInput } };

            RunAndVerifyAttackFlag(pathArgument, getFullPathMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_GetFullPathWithNormalizedAbsolutePayload_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var getFullPathMethodInfo = typeof(Path).GetMethod("GetFullPath", new[] { typeof(string) })!;
            var unsafeInput = "/././etc/passwd";
            var pathArgument = Path.Combine("wwwroot/blogs", unsafeInput);
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.path", unsafeInput } };

            RunAndVerifyAttackFlag(pathArgument, getFullPathMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_GetFullPathWithBasePath_ReturnsTrueForSafePath()
        {
            var getFullPathMethodInfo = typeof(Path).GetMethod("GetFullPath", new[] { typeof(string), typeof(string) })!;

            var result = OnFileOperationPathWithBasePath(
                "child.txt",
                Path.GetTempPath(),
                getFullPathMethodInfo,
                _mockContext.Object);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnFileOperation_AbsoluteInput_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var absoluteInput = "/etc/shadow";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };
            RunAndVerifyAttackFlag(absoluteInput, _methodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_AbsoluteInput_ReturnsTrueWhenNotBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var absoluteInput = "/etc/shadow";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "path", absoluteInput } };
            RunAndVerifyAttackFlag(absoluteInput, _methodInfo, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void OnFileOperation_DirectoryGetFilesWithUserInputPathTraversal_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var getFilesMethodInfo = typeof(Directory).GetMethod("GetFiles", new[] { typeof(string) })!;
            var unsafeInput = "../secrets";
            var pathArgument = $"/var/www/{unsafeInput}";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "query.dir", unsafeInput } };

            RunAndVerifyAttackFlag(pathArgument, getFilesMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_DirectoryGetFilesWithAbsoluteUserInput_ThrowsExceptionWhenBlocking()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var getFilesMethodInfo = typeof(Directory).GetMethod("GetFiles", new[] { typeof(string) })!;
            var absoluteInput = "/etc/some_directory";
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "body.file.matches", absoluteInput } };

            RunAndVerifyAttackFlag(absoluteInput, getFilesMethodInfo, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void OnFileOperation_WithForceProtectionOffRoute_ReturnsTrueWithoutMarkingAttack()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../secrets/key.txt";
            var contextToPass = _mockContext.Object;

            contextToPass.Method = "GET";
            contextToPass.Route = "/api/read";
            contextToPass.Path = "/api/read";
            contextToPass.ParsedUserInput = new Dictionary<string, string> { { "query.path", unsafeInput } };
            contextToPass.AttackDetected = false;

            Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/api/read",
                    ForceProtectionOff = true
                }
            });

            var result = OnFileOperation($"/var/www/data/{unsafeInput}", _methodInfo, contextToPass);

            Assert.That(result, Is.True);
            Assert.That(contextToPass.AttackDetected, Is.False);
        }

        [Test]
        public void OnFileOperation_WithMalformedEndpointConfig_ReturnsTrueWithoutMarkingAttack()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var unsafeInput = "../secrets/key.txt";
            var contextToPass = _mockContext.Object;

            contextToPass.Method = "GET";
            contextToPass.Route = "/api/read";
            contextToPass.Path = "/api/read";
            contextToPass.ParsedUserInput = new Dictionary<string, string> { { "query.path", unsafeInput } };
            contextToPass.AttackDetected = false;

#pragma warning disable CS8625
            Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = null,
                    Route = "/api/read",
                    ForceProtectionOff = true
                }
            });
#pragma warning restore CS8625

            var result = OnFileOperation($"/var/www/data/{unsafeInput}", _methodInfo, contextToPass);

            Assert.That(result, Is.True);
            Assert.That(contextToPass.AttackDetected, Is.False);
        }

        [Test]
        public void OnFileOperation_WhenUriExceptionResolutionTouchesFileExists_DoesNotReenterIndefinitely()
        {
            var contextToPass = _mockContext.Object;
            contextToPass.Method = "GET";
            contextToPass.Route = "/api/read";
            contextToPass.Path = "http://[::1";
            contextToPass.ParsedUserInput = new Dictionary<string, string>();
            contextToPass.AttackDetected = false;

            Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/api/*",
                    ForceProtectionOff = false
                }
            });

            var resolvingCalls = 0;
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUICulture = CultureInfo.CurrentUICulture;

            Assembly? ResolveByCheckingFileExists(AssemblyLoadContext context, AssemblyName assemblyName)
            {
                resolvingCalls++;
                File.Exists("missing-resource-satellite.dll");
                return null;
            }

            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            AssemblyLoadContext.Default.Resolving += ResolveByCheckingFileExists;

            try
            {
                _activeContext = contextToPass;
                Assert.That(Path.GetFullPath("safe.txt"), Is.Not.Empty);
            }
            finally
            {
                AssemblyLoadContext.Default.Resolving -= ResolveByCheckingFileExists;
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }

            Assert.That(resolvingCalls, Is.GreaterThan(0));
            Assert.That(contextToPass.AttackDetected, Is.False);
        }

        private bool OnFileOperation(string path, MethodInfo methodInfo, Context? context)
        {
            _activeContext = context;
            return IOSink.OnFileOperationOnePath(path, methodInfo);
        }

        private bool OnFileOperationTwoPaths(string sourceFileName, string destFileName, MethodInfo methodInfo, Context? context)
        {
            _activeContext = context;
            return IOSink.OnFileOperationTwoPaths(sourceFileName, destFileName, methodInfo);
        }

        private bool OnFileOperationPathWithBasePath(string path, string basePath, MethodInfo methodInfo, Context? context)
        {
            _activeContext = context;
            return IOSink.OnFileOperationPathWithBasePath(path, basePath, methodInfo);
        }
    }
}
