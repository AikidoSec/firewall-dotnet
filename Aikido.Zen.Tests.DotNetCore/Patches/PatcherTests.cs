using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Tests
{
    /// <summary>
    /// Tests for the <see cref="IOPatcher"/> class.
    /// </summary>
    [TestFixture]
    public class PatcherTests
    {
        /// <summary>
        /// Verifies that the ShouldExcludeAssembly method correctly identifies assemblies that should be excluded.
        /// </summary>
        /// <param name="assemblyName">The full name of the assembly to check.</param>
        /// <param name="expected">The expected result.</param>
        [TestCase("Costura, Version=4.1.0.0, Culture=neutral, PublicKeyToken=9919ef960d84173d", true)]
        [TestCase("0Harmony, Version=2.2.2.0, Culture=neutral, PublicKeyToken=null", true)]
        [TestCase("Fody, Version=6.5.1.0, Culture=neutral, PublicKeyToken=a750436ab3144e16", true)]
        [TestCase("Mono.Cecil, Version=0.11.4.0, Culture=neutral, PublicKeyToken=50cebf1cceb9d05e", true)]
        [TestCase("PostSharp, Version=6.10.16.0, Culture=neutral, PublicKeyToken=b13fd38b8f9c99d7", true)]
        [TestCase("System.Runtime, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false)]
        [TestCase("MyWebApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
        [TestCase("Another.Dependency, Version=2.3.4.5, Culture=neutral, PublicKeyToken=abcdef1234567890", false)]
        [TestCase("Aikido.Zen.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
        [TestCase("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false)]
        public void ShouldExcludeAssembly_ShouldReturnCorrectResult(string assemblyName, bool expected)
        {
            // Act
            var result = ReflectionHelper.ShouldExcludeAssembly(assemblyName);

            // Assert
            Assert.AreEqual(expected, result);
        }
    }
}
