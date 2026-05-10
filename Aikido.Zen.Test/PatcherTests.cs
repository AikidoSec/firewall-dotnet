using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using HarmonyLib;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class PatcherTests
    {
        private const string HarmonyId = "aikido.zen";
        private Context _context;
        private Agent _agent;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context
            {
                Method = "GET",
                Route = "/safe",
                Path = "/safe",
                Url = "https://app.local/safe",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>()
            };

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3000");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            _agent = Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            _agent.ClearContext();
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_URL", null);
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", null);
        }

        [Test]
        public void PublicPatchMethods_DoNotThrowWhenOptionalTargetsAreMissing()
        {
            Assert.DoesNotThrow(() => Patcher.PatchSinks(() => null));
            Assert.DoesNotThrow(() => Patcher.Unpatch());
            Assert.DoesNotThrow(() => Patcher.PatchSinks(() => _context));
            Assert.DoesNotThrow(() => Patcher.Unpatch());
            Assert.DoesNotThrow(() => Patcher.Unpatch());
        }

        [Test]
        public void Patch_AppliesSharedDefinitionsToAvailableRuntimeMethods()
        {
            Patcher.PatchSinks(() => _context);

            AssertPrefixPatch(GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string)));
            AssertPrefixPatch(GetMethod(typeof(File), nameof(File.Copy), typeof(string), typeof(string), typeof(bool)));
            AssertPrefixPatch(GetMethod(typeof(Path), nameof(Path.GetFullPath), typeof(string), typeof(string)));
            AssertPrefixPatch(GetMethod(
                typeof(HttpClient),
                nameof(HttpClient.SendAsync),
                typeof(HttpRequestMessage),
                typeof(CancellationToken)));
            AssertPrefixPatch(GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalarAsync)));
        }

        [Test]
        public void Patch_WhenContextProviderIsNull_UsesNullContext()
        {
#pragma warning disable CS8625
            Patcher.PatchSinks(null);
#pragma warning restore CS8625

            Assert.That(Patcher.GetContext(), Is.Null);
        }

        [Test]
        public void TypedSinkMethods_UseConfiguredContextAndReturnTrueForSafeCalls()
        {
            Patcher.PatchSinks(() => _context);

            Assert.That(IOSink.OnFileOperation("safe.txt", GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string)), _context), Is.True);
            Assert.That(IOSink.OnFileOperation(null, GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string)), _context), Is.True);
            Assert.That(IOSink.OnFileOperation("source.txt", GetMethod(typeof(File), nameof(File.Copy), typeof(string), typeof(string), typeof(bool)), _context), Is.True);
            Assert.That(IOSink.OnFileOperation("dest.txt", GetMethod(typeof(File), nameof(File.Copy), typeof(string), typeof(string), typeof(bool)), _context), Is.True);

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://safe.example") })
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/path");
                Assert.That(OutboundRequestSink.OnRequest(
                    new Uri(httpClient.BaseAddress, request.RequestUri!),
                    GetMethod(typeof(HttpClient), nameof(HttpClient.SendAsync), typeof(HttpRequestMessage), typeof(CancellationToken)),
                    _context), Is.True);
            }

            Assert.That(OutboundRequestSink.OnRequest(
                null,
                GetMethod(typeof(HttpClient), nameof(HttpClient.SendAsync), typeof(HttpRequestMessage), typeof(CancellationToken)),
                _context), Is.True);

#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://safe.example/path");
#pragma warning restore SYSLIB0014
            Assert.That(OutboundRequestSink.OnRequest(
                webRequest.RequestUri,
                GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse)),
                _context), Is.True);

            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");
            Assert.That(SqlClientSink.OnCommandExecuting(
                dbCommand.Object.CommandText,
                GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalar)),
                _context), Is.True);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo("echo", "safe")
            };
            Assert.That(ProcessExecutionSink.OnProcessStart(
                process,
                GetMethod(typeof(Process), nameof(Process.Start)),
                _context), Is.True);

            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(
                null,
                null,
                GetMethod(typeof(object), nameof(ToString)),
                _context));
        }

        [Test]
        public void PatchCatalog_SkipsMissingTargetsAndStillPatchesValidTargets()
        {
            Assert.DoesNotThrow(() => Patcher.PatchCatalog(typeof(ScannerCatalog)));

            Assert.That(ScannerTarget.PrefixTarget(), Is.EqualTo("prefix"));
            Assert.That(ScannerTarget.PostfixTarget(), Is.EqualTo("postfix"));
            AssertPrefixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PrefixTarget)));
            AssertPostfixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PostfixTarget)));
        }

        [Test]
        public void PatchedFileApis_BlockPathTraversalBeforeOriginalOperation()
        {
            Patcher.PatchSinks(() => _context);
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"aikido-patcher-{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(tempDirectory);
                var sourcePath = Path.Combine(tempDirectory, "source.txt");
                var destinationPath = Path.Combine(tempDirectory, "destination.txt");

                File.WriteAllText(sourcePath, "safe");
                Assert.That(File.ReadAllText(sourcePath), Is.EqualTo("safe"));

                File.Copy(sourcePath, destinationPath, true);
                Assert.That(File.ReadAllText(destinationPath), Is.EqualTo("safe"));

                Assert.That(
                    Path.GetFullPath("child.txt", tempDirectory),
                    Is.EqualTo(Path.Combine(tempDirectory, "child.txt")));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.path", "../secret.txt" }
            };

            var readException = Assert.Throws<AikidoException>(() =>
                File.ReadAllText("/var/www/data/../secret.txt"));
            Assert.That(readException?.Message, Does.Contain("Path traversal detected"));

            var copyException = Assert.Throws<AikidoException>(() =>
                File.Copy("/var/www/data/source.txt", "/var/www/data/../secret.txt", true));
            Assert.That(copyException?.Message, Does.Contain("Path traversal detected"));

            var fullPathException = Assert.Throws<AikidoException>(() =>
                Path.GetFullPath("safe.txt", "/var/www/data/../secret.txt"));
            Assert.That(fullPathException?.Message, Does.Contain("Path traversal detected"));
        }

        [Test]
        public void PatchedHttpApis_BlockOutboundRequestBeforeNetworkCall()
        {
            Patcher.PatchSinks(() => _context);

            using (var safeHttpClient = new HttpClient(new TestHttpMessageHandler()))
            using (var safeRequest = new HttpRequestMessage(HttpMethod.Get, "https://safe.example/path"))
            {
                var response = safeHttpClient.Send(safeRequest, CancellationToken.None);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }

            using (var clientWithBaseAddress = new HttpClient(new TestHttpMessageHandler()) { BaseAddress = new Uri("https://safe.example") })
            {
                var exception = Assert.Throws<ArgumentNullException>(() =>
                    clientWithBaseAddress.Send(null!, CancellationToken.None));
                Assert.That(exception?.ParamName, Is.EqualTo("request"));
            }

            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://blocked.example") })
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/path"))
            {
                var httpClientException = Assert.Throws<AikidoException>(() =>
                    httpClient.Send(request, CancellationToken.None));
                Assert.That(httpClientException?.Message, Does.Contain("blocked.example"));
            }

#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://blocked.example/path");
#pragma warning restore SYSLIB0014
            webRequest.Timeout = 1;

            var webRequestException = Assert.Throws<AikidoException>(() => webRequest.GetResponse());
            Assert.That(webRequestException?.Message, Does.Contain("blocked.example"));
        }

        [Test]
        public void PatchedProcessStart_BlocksShellInjectionBeforeStartingProcess()
        {
            Patcher.PatchSinks(() => _context);

            using (var safeProcess = new Process
            {
                StartInfo = new ProcessStartInfo("definitely-not-a-real-aikido-command", "--safe")
            })
            {
                Assert.Throws<System.ComponentModel.Win32Exception>(() => safeProcess.Start());
            }

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.command", "$(echo)" }
            };

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("definitely-not-a-real-aikido-command", "$(echo)")
            };

            var exception = Assert.Throws<AikidoException>(() => process.Start());
            Assert.That(exception?.Message, Does.Contain("Shell injection detected"));
        }

        [Test]
        public void PatchedDbCommandAsync_BlocksSqlInjectionBeforeExecutingCommand()
        {
            Patcher.PatchSinks(() => _context);

            using (var safeCommand = new TestDbCommand { CommandText = "SELECT 1" })
            {
                Assert.DoesNotThrowAsync(async () => await safeCommand.ExecuteScalarAsync());
            }

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1'" }
            };

            using var command = new TestDbCommand
            {
                CommandText = "SELECT * FROM users WHERE id = '1' OR '1'='1'"
            };

            var exception = Assert.ThrowsAsync<AikidoException>(async () => await command.ExecuteScalarAsync());
            Assert.That(exception?.Message, Does.Contain("SQL injection detected"));
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

        private void AssertPrefixPatch(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Prefixes.Any(prefix => prefix.owner == HarmonyId), Is.True);
        }

        private void AssertPostfixPatch(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Postfixes.Any(postfix => postfix.owner == HarmonyId), Is.True);
        }

        private static class ScannerTarget
        {
            public static string PrefixTarget()
            {
                return "original";
            }

            public static string PostfixTarget()
            {
                return "original";
            }
        }

        private sealed class TestDbCommand : DbCommand
        {
            private readonly DbParameterCollection _parameters = new Mock<DbParameterCollection>().Object;
            private string _commandText = string.Empty;

            [System.Diagnostics.CodeAnalysis.AllowNull]
            public override string CommandText
            {
                get => _commandText;
                set => _commandText = value ?? string.Empty;
            }

            public override int CommandTimeout { get; set; }
            public override System.Data.CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => _parameters;
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery()
            {
                return 0;
            }

            public override object? ExecuteScalar()
            {
                return null;
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter()
            {
                throw new NotSupportedException();
            }

            protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request
                };
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request, cancellationToken));
            }
        }

        private static class ScannerCatalog
        {
            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+ScannerTarget", nameof(ScannerTarget.PrefixTarget))]
            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+ScannerTarget", "MissingTarget")]
            private static bool Prefix(ref string __result)
            {
                __result = "prefix";
                return false;
            }

            [SinkPostfix("", "Aikido.Zen.Test.PatcherTests+ScannerTarget", nameof(ScannerTarget.PostfixTarget))]
            private static void Postfix(ref string __result)
            {
                __result = "postfix";
            }
        }

    }
}
