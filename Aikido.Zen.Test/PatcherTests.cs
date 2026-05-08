using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
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
        public void SinkWrappers_UseConfiguredContextAndReturnTrueForSafeCalls()
        {
            Patcher.PatchSinks(() => _context);

            Assert.That(IOSink.OnPathOperation(new object[] { "safe.txt" }, GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string))), Is.True);
            Assert.That(IOSink.OnPathOperation(null, GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string))), Is.True);
            Assert.That(IOSink.OnTwoPathOperation(new object[] { "source.txt", "dest.txt", true }, GetMethod(typeof(File), nameof(File.Copy), typeof(string), typeof(string), typeof(bool))), Is.True);

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://safe.example") })
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/path");
                Assert.That(OutboundRequestSink.OnRequest(
                    new object[] { request, CancellationToken.None },
                    GetMethod(typeof(HttpClient), nameof(HttpClient.SendAsync), typeof(HttpRequestMessage), typeof(CancellationToken)),
                    httpClient), Is.True);
            }

            Assert.That(OutboundRequestSink.OnRequest(
                null,
                GetMethod(typeof(HttpClient), nameof(HttpClient.SendAsync), typeof(HttpRequestMessage), typeof(CancellationToken)),
                null), Is.True);

#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://safe.example/path");
#pragma warning restore SYSLIB0014
            Assert.That(OutboundRequestSink.OnRequest(
                Array.Empty<object>(),
                GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse)),
                webRequest), Is.True);

            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");
            Assert.That(SqlClientSink.OnCommandExecuting(
                Array.Empty<object>(),
                GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalar)),
                dbCommand.Object), Is.True);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo("echo", "safe")
            };
            Assert.That(ProcessExecutionSink.OnProcessStart(
                Array.Empty<object>(),
                GetMethod(typeof(Process), nameof(Process.Start)),
                process), Is.True);

            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(
                Array.Empty<object>(),
                GetMethod(typeof(object), nameof(ToString)),
                new object(),
                null));
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

    }
}
