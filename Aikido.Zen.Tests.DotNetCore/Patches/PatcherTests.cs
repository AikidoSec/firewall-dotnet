using System.Reflection;
using System.Reflection.Emit;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Tests.Helpers;

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
        [TestCase("dnlib, Version=4.5.0.0, Culture=neutral, PublicKeyToken=50e96378b6e77999", true)]
        [TestCase("ILRepack, Version=2.0.44, Culture=neutral, PublicKeyToken=null", true)]
        [TestCase("BepInEx.AssemblyPublicizer.MSBuild, Version=0.4.3.0, Culture=neutral, PublicKeyToken=null", true)]
        [TestCase("Castle.DynamicProxy2, Version=2.2.0.0, Culture=neutral, PublicKeyToken=407dd0808d44fbdc", true)]
        [TestCase("Autofac.Extras.DynamicProxy, Version=7.1.0.0, Culture=neutral, PublicKeyToken=17863af14b0044da", true)]
        [TestCase("System.Reflection.DispatchProxy, Version=4.0.6.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", true)]
        [TestCase("Datadog.Trace.Manual, Version=3.27.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", true)]
        [TestCase("NewRelic.Agent.Core, Version=10.0.0.0, Culture=neutral, PublicKeyToken=06552fced0b33d87", true)]
        [TestCase("OpenTelemetry, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c", true)]
        [TestCase("CodeAnalysis, Version=4.14.0, Culture=neutral, PublicKeyToken=null", true)]
        [TestCase("ScriptCs.Core, Version=0.17.0.0, Culture=neutral, PublicKeyToken=null", true)]
        [TestCase("Jint, Version=4.4.1.0, Culture=neutral, PublicKeyToken=2e92ba9c8d81157f", true)]
        [TestCase("ClearScript.Core, Version=7.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35", true)]
        [TestCase("System.Reflection.Emit, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", true)]
        [TestCase("System.Runtime, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", false)]
        [TestCase("MyWebApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
        [TestCase("Another.Dependency, Version=2.3.4.5, Culture=neutral, PublicKeyToken=abcdef1234567890", false)]
        [TestCase("Aikido.Zen.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false)]
        [TestCase("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false)]
        public void ShouldExcludeAssembly_ShouldReturnCorrectResult(string assemblyName, bool expected)
        {
            //Arrange
            // Use AssemblyName to parse the full name
            var name = new AssemblyName(assemblyName);
            // Dynamically define an Assembly using reflection emit
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

            // Act
            var result = ReflectionHelper.ShouldExcludeAssembly(assembly);

            // Assert
            Assert.AreEqual(expected, result);
        }
    }
}
