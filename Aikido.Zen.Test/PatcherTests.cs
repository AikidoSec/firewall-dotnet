using System.Data.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Sinks;
using HarmonyLib;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class PatcherTests
    {
        private const string HarmonyId = "aikido.zen";
        private Context _context = null!;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
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
            AssertFinalizerPatch(GetMethod(typeof(Dns), nameof(Dns.GetHostAddresses), typeof(string)));
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
            Assert.That(ScannerTarget.FinalizerTarget(), Is.EqualTo("finalizer"));
            AssertPrefixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PrefixTarget)));
            AssertPostfixPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.PostfixTarget)));
            AssertFinalizerPatch(GetMethod(typeof(ScannerTarget), nameof(ScannerTarget.FinalizerTarget)));
        }

        [Test]
        public void PatchCatalog_WithSinkFinalizer_PatchesEveryPrefixTarget()
        {
            Patcher.PatchCatalog(typeof(SinkFinalizerCatalog));

            Assert.That(SinkFinalizerTarget.First(), Is.EqualTo("sink-finalizer"));
            Assert.That(SinkFinalizerTarget.Second(), Is.EqualTo("sink-finalizer"));
            Assert.That(SinkFinalizerTarget.PostfixOnly(), Is.EqualTo("postfix-only"));

            AssertFinalizerPatch(GetMethod(typeof(SinkFinalizerTarget), nameof(SinkFinalizerTarget.First)));
            AssertFinalizerPatch(GetMethod(typeof(SinkFinalizerTarget), nameof(SinkFinalizerTarget.Second)));
            AssertNoFinalizerPatch(GetMethod(typeof(SinkFinalizerTarget), nameof(SinkFinalizerTarget.PostfixOnly)));
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
        public void PatchCatalog_WithTypeTarget_PatchesRuntimeTypeDirectly()
        {
            Patcher.PatchCatalog(typeof(TypeTargetCatalog));

            Assert.That(TypeTarget.PrefixTarget(), Is.EqualTo("type-target"));
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

        private static void AssertFinalizerPatch(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Finalizers.Any(finalizer => finalizer.owner == HarmonyId), Is.True);
        }

        private static void AssertNoFinalizerPatch(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches == null || !patches.Finalizers.Any(finalizer => finalizer.owner == HarmonyId), Is.True);
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

            public static string FinalizerTarget()
            {
                return "original";
            }
        }

        private static class ScannerCatalog
        {
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+ScannerTarget", nameof(ScannerTarget.PrefixTarget))]
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+ScannerTarget", "MissingTarget")]
            private static bool Prefix(ref string __result)
            {
                __result = "prefix";
                return false;
            }

            [SinkPostfix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+ScannerTarget", nameof(ScannerTarget.PostfixTarget))]
            private static void Postfix(ref string __result)
            {
                __result = "postfix";
            }

            [SinkFinalizer("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+ScannerTarget", nameof(ScannerTarget.FinalizerTarget))]
            private static Exception Finalizer(ref string __result)
            {
                __result = "finalizer";
                return null;
            }
        }

        private static class SinkFinalizerTarget
        {
            public static string First()
            {
                return "first";
            }

            public static string Second()
            {
                return "second";
            }

            public static string PostfixOnly()
            {
                return "postfix-original";
            }
        }

        private static class SinkFinalizerCatalog
        {
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+SinkFinalizerTarget", nameof(SinkFinalizerTarget.First))]
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+SinkFinalizerTarget", nameof(SinkFinalizerTarget.Second))]
            private static void Prefix()
            {
            }

            [SinkPostfix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+SinkFinalizerTarget", nameof(SinkFinalizerTarget.PostfixOnly))]
            private static void Postfix(ref string __result)
            {
                __result = "postfix-only";
            }

            [SinkFinalizer]
            private static Exception Finalizer(ref string __result)
            {
                __result = "sink-finalizer";
                return null!;
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
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+BrokenPatchTarget", nameof(BrokenPatchTarget.ValidTarget))]
            private static bool BrokenPrefix(string missingArgument)
            {
                return true;
            }

            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+BrokenPatchTarget", nameof(BrokenPatchTarget.ValidTarget))]
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

            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+UnsafeAssemblyTarget", nameof(UnsafeAssemblyTarget.ValidTarget))]
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
            [SinkPrefix("Aikido.Zen.Tests", "Aikido.Zen.Test.PatcherTests+OverloadFallbackTarget", nameof(OverloadFallbackTarget.Execute))]
            private static bool Prefix(ref string __result)
            {
                __result = "patched-overload";
                return false;
            }
        }

        private static class TypeTarget
        {
            public static string PrefixTarget()
            {
                return "original";
            }
        }

        private static class TypeTargetCatalog
        {
            [SinkPrefix(typeof(TypeTarget), "PrefixTarget")]
            private static bool Prefix(ref string __result)
            {
                __result = "type-target";
                return false;
            }
        }

    }
}
