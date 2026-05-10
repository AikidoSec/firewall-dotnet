using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
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
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3001");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

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
        public void PatchSinks_DoesNotThrowWhenOptionalTargetsAreMissing()
        {
            Assert.DoesNotThrow(() => Patcher.PatchSinks(() => _context));
            Assert.DoesNotThrow(() => Patcher.Unpatch());
            Assert.DoesNotThrow(() => Patcher.Unpatch());
        }

        [Test]
        public void PatchSinks_AppliesRepresentativeRuntimePatches()
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
        public void PatchSinks_WhenContextProviderIsNull_UsesNullContext()
        {
#pragma warning disable CS8625
            Patcher.PatchSinks(null);
#pragma warning restore CS8625

            Assert.That(Patcher.GetContext(), Is.Null);
        }

        [Test]
        public void PatchCatalog_SkipsMissingTargetsAndAppliesPrefixAndPostfix()
        {
            Assert.DoesNotThrow(() => Patcher.PatchCatalog(typeof(ScannerCatalog)));

            Assert.That(ScannerTarget.PrefixTarget(), Is.EqualTo("prefix"));
            Assert.That(ScannerTarget.PostfixTarget(), Is.EqualTo("postfix"));
            AssertPrefixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PrefixTarget)));
            AssertPostfixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PostfixTarget)));
        }

        [Test]
        public void PatchCatalog_ContinuesWhenPatchApplicationFails()
        {
            Assert.DoesNotThrow(() => Patcher.PatchCatalog(typeof(BrokenPatchCatalog)));

            Assert.That(BrokenPatchTarget.ValidTarget(), Is.EqualTo("patched-after-error"));
        }

        [Test]
        public void PatchCatalog_SkipsUnsafeAssemblyNamesAndStillPatchesValidTarget()
        {
            Assert.DoesNotThrow(() => Patcher.PatchCatalog(typeof(UnsafeAssemblyCatalog)));

            Assert.That(UnsafeAssemblyTarget.ValidTarget(), Is.EqualTo("patched-after-unsafe-assembly"));
        }

        [Test]
        public void PatchCatalog_WhenParameterTypesAreNotDeclared_PatchesLargestOverload()
        {
            Patcher.PatchCatalog(typeof(OverloadFallbackCatalog));

            Assert.That(OverloadFallbackTarget.Execute("value"), Is.EqualTo("one-argument"));
            Assert.That(OverloadFallbackTarget.Execute("value", 1), Is.EqualTo("patched-overload"));
        }

        [Test]
        public void PatchCatalog_ForwardsIoArgumentsToSink()
        {
            Patcher.PatchSinks(() => _context);

            Assert.That(IOPatches.OnePath(
                "safe.txt",
                GetMethod(typeof(File), nameof(File.ReadAllText), typeof(string))), Is.True);
            Assert.That(IOPatches.TwoFilePaths(
                "source.txt",
                "destination.txt",
                GetMethod(typeof(File), nameof(File.Copy), typeof(string), typeof(string), typeof(bool))), Is.True);
            Assert.That(IOPatches.PathWithBasePath(
                "child.txt",
                Path.GetTempPath(),
                GetMethod(typeof(Path), nameof(Path.GetFullPath), typeof(string), typeof(string))), Is.True);
        }

        [Test]
        public void PatchCatalog_ForwardsOutboundArgumentsToSink()
        {
            Patcher.PatchSinks(() => _context);
            var method = GetMethod(
                typeof(HttpClient),
                nameof(HttpClient.SendAsync),
                typeof(HttpRequestMessage),
                typeof(CancellationToken));

            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://safe.example/path"))
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(request, httpClient, method), Is.True);
            }

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://base.example") })
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/relative"))
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(request, httpClient, method), Is.True);
            }

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://base-only.example") })
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(null!, httpClient, method), Is.True);
            }

#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://safe.example/path");
#pragma warning restore SYSLIB0014
            Assert.That(OutboundRequestPatches.WebRequest(webRequest, GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse))), Is.True);
        }

        [Test]
        public void PatchCatalog_ForwardsSqlProcessAndLlmArgumentsToSinks()
        {
            Patcher.PatchSinks(() => _context);

            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");
            var dbMethod = GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalar));

            Assert.That(SqlClientPatches.DbCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.NPocoCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.ExecuteSqlRaw(
                "SELECT 1",
                GetMethod(typeof(TestSqlMethods), nameof(TestSqlMethods.ExecuteSqlRaw), typeof(object), typeof(string), typeof(IEnumerable<object>))), Is.True);

            using (var process = new Process { StartInfo = new ProcessStartInfo("echo", "safe") })
            {
                Assert.That(ProcessExecutionPatches.ProcessStart(
                    process,
                    GetMethod(typeof(Process), nameof(Process.Start))), Is.True);
            }

            Assert.DoesNotThrow(() => LLMPatches.LLMCallCompleted(
                null!,
                null!,
                GetMethod(typeof(object), nameof(ToString))));
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

        private static void AssertPrefixPatch(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Prefixes.Any(prefix => prefix.owner == HarmonyId), Is.True);
        }

        private static void AssertPostfixPatch(MethodInfo method)
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

        private static class BrokenPatchTarget
        {
            public static string ValidTarget()
            {
                return "original";
            }
        }

        private static class BrokenPatchCatalog
        {
            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+BrokenPatchTarget", nameof(BrokenPatchTarget.ValidTarget))]
            private static bool BrokenPrefix(string missingArgument)
            {
                return true;
            }

            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+BrokenPatchTarget", nameof(BrokenPatchTarget.ValidTarget))]
            private static bool ValidPrefix(ref string __result)
            {
                __result = "patched-after-error";
                return false;
            }
        }

        private static class UnsafeAssemblyTarget
        {
            public static string ValidTarget()
            {
                return "original";
            }
        }

        private static class UnsafeAssemblyCatalog
        {
            [SinkPrefix("../unsafe", "Aikido.Zen.Test.PatcherTests+UnsafeAssemblyTarget", nameof(UnsafeAssemblyTarget.ValidTarget))]
            private static bool UnsafeAssemblyPrefix(ref string __result)
            {
                __result = "should-not-patch";
                return false;
            }

            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+UnsafeAssemblyTarget", nameof(UnsafeAssemblyTarget.ValidTarget))]
            private static bool ValidPrefix(ref string __result)
            {
                __result = "patched-after-unsafe-assembly";
                return false;
            }
        }

        private static class OverloadFallbackTarget
        {
            public static string Execute(string value)
            {
                return "one-argument";
            }

            public static string Execute(string value, int count)
            {
                return "two-arguments";
            }
        }

        private static class OverloadFallbackCatalog
        {
            [SinkPrefix("", "Aikido.Zen.Test.PatcherTests+OverloadFallbackTarget", nameof(OverloadFallbackTarget.Execute))]
            private static bool Prefix(ref string __result)
            {
                __result = "patched-overload";
                return false;
            }
        }

        private static class TestSqlMethods
        {
            public static int ExecuteSqlRaw(object databaseFacade, string sql, IEnumerable<object> parameters)
            {
                return 0;
            }
        }
    }
}
