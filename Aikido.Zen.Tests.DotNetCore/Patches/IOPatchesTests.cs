using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using Aikido.Zen.DotNetCore.Patches;
using HarmonyLib;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore.Patches
{
    [TestFixture]
    public class IOPatchesTests
    {
        private Harmony _harmony;
        private static readonly string HarmonyId = "com.aikido.zen.tests.dotnetcore.patches";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _harmony = new Harmony(HarmonyId);
            IOPatches.ApplyPatches(_harmony);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _harmony.UnpatchAll(HarmonyId);
        }

        [TestCase(typeof(File), "Open", new[] { typeof(string), typeof(FileMode) })]
        [TestCase(typeof(File), "OpenRead", new[] { typeof(string) })]
        [TestCase(typeof(File), "OpenWrite", new[] { typeof(string) })]
        [TestCase(typeof(File), "Create", new[] { typeof(string), typeof(Int32), typeof(FileOptions) })]
        [TestCase(typeof(File), "Delete", new[] { typeof(string) })]
        [TestCase(typeof(File), "Copy", new[] { typeof(string), typeof(string), typeof(bool) })]
        [TestCase(typeof(File), "Move", new[] { typeof(string), typeof(string), typeof(bool) })]
        [TestCase(typeof(File), "ReadAllText", new[] { typeof(string) })]
        [TestCase(typeof(File), "ReadAllBytes", new[] { typeof(string) })]
        [TestCase(typeof(File), "WriteAllText", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(File), "WriteAllBytes", new[] { typeof(string), typeof(byte[]) })]
        [TestCase(typeof(File), "AppendAllText", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(Directory), "CreateDirectory", new[] { typeof(string) })]
        [TestCase(typeof(Directory), "Delete", new[] { typeof(string), typeof(bool) })]
        [TestCase(typeof(Directory), "GetFiles", new[] { typeof(string) })]
        [TestCase(typeof(Directory), "GetFiles", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(Directory), "GetFiles", new[] { typeof(string), typeof(string), typeof(SearchOption) })]
        [TestCase(typeof(Directory), "GetDirectories", new[] { typeof(string) })]
        [TestCase(typeof(Directory), "GetDirectories", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(Directory), "GetDirectories", new[] { typeof(string), typeof(string), typeof(SearchOption) })]
        public void Method_IsPatched(Type type, string methodName, Type[] parameters)
        {
            var method = type.GetMethod(methodName, parameters);
            Assert.IsNotNull(method, $"Method {methodName} not found on type {type.Name}");

            var patches = Harmony.GetPatchInfo(method);
            Assert.IsNotNull(patches, "Harmony patches should exist.");
            Assert.IsTrue(patches.Prefixes.Any(p => p.owner == HarmonyId), "Our prefix should be applied.");
        }
    }
}
