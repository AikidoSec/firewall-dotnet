using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using Aikido.Zen.DotNetFramework.Patches;
using HarmonyLib;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetFramework.Patches
{
    [TestFixture]
    public class IOPatchesTests
    {
        private Harmony _harmony;
        private static readonly string HarmonyId = "com.aikido.zen.tests.dotnetframework.patches";

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
        [TestCase(typeof(File), "Create", new[] { typeof(string), typeof(int), typeof(FileOptions) })]
        [TestCase(typeof(File), "Delete", new[] { typeof(string) })]
        [TestCase(typeof(File), "Copy", new[] { typeof(string), typeof(string), typeof(bool) })]
        [TestCase(typeof(File), "Move", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(File), "ReadAllText", new[] { typeof(string) })]
        [TestCase(typeof(File), "ReadAllBytes", new[] { typeof(string) })]
        [TestCase(typeof(File), "WriteAllText", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(File), "WriteAllBytes", new[] { typeof(string), typeof(byte[]) })]
        [TestCase(typeof(File), "AppendAllText", new[] { typeof(string), typeof(string) })]
        [TestCase(typeof(Directory), "CreateDirectory", new[] { typeof(string), typeof(DirectorySecurity) })]
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
            Assert.That(method, Is.Not.Null, $"Method {methodName} not found on type {type.Name} with specified parameters.");

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist.");
            Assert.That(patches.Prefixes.Any(p => p.owner == HarmonyId), Is.True, "Our prefix should be applied.");
        }

        [Test]
        public void Patched_Method_Does_Not_Throw()
        {
            var testDirPath = Path.Combine(Path.GetTempPath(), "test-dir-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var testFilePath = Path.Combine(testDirPath, "test-");

            try
            {
                // Create test directory first
                Directory.CreateDirectory(testDirPath);

                // file creation
                File.Create(testFilePath + "1.txt").Dispose();
                File.CreateText(testFilePath + "2.txt").Dispose();

                // open file
                File.Open(testFilePath + "1.txt", FileMode.Open).Dispose();
                File.OpenRead(testFilePath + "1.txt").Dispose();
                File.OpenWrite(testFilePath + "1.txt").Dispose();

                // read file (before deleting)
                File.ReadAllText(testFilePath + "1.txt");
                File.ReadAllBytes(testFilePath + "1.txt");

                // write file operations
                File.WriteAllText(testFilePath + "1.txt", "test content");
                File.WriteAllBytes(testFilePath + "1.txt", new byte[] { 1, 2, 3 });

                // append file
                File.AppendAllText(testFilePath + "1.txt", " appended text");

                // copy file
                File.Copy(testFilePath + "2.txt", testFilePath + "2.txt.copy");
                File.Delete(testFilePath + "2.txt.copy");

                // move file
                File.Move(testFilePath + "2.txt", testFilePath + "2.txt.move");

                // delete files
                File.Delete(testFilePath + "1.txt");
                File.Delete(testFilePath + "2.txt.move");

                // Directory operations
                Directory.CreateDirectory(Path.Combine(testDirPath, "subdir"));
                Directory.GetDirectories(testDirPath);
                Directory.GetFiles(testDirPath, "test-*");
                Directory.GetDirectories(testDirPath, "sub*");
                // Directory deletion
                Directory.Delete(Path.Combine(testDirPath, "subdir"));
                Directory.Exists(Path.Combine(testDirPath, "subdir"));
                // Directory exists
                Directory.Exists(testDirPath);
            }
            finally
            {
                // cleanup - delete the entire test directory and all contents
                if (Directory.Exists(testDirPath))
                {
                    Directory.Delete(testDirPath, true);
                }
            }
        }
    }
}
