using System.Reflection;
using Aikido.Zen.Core.Sinks;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class PatchDefinitionTests
    {
        [Test]
        public void Prefix_WithSingleAssemblyName_CreatesPrefixDefinition()
        {
            var definition = PatchDefinition.Prefix(
                PatchMethod,
                "Npgsql",
                "NpgsqlCommand",
                "ExecuteScalar",
                "System.Threading.CancellationToken");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Prefix));
            Assert.That(definition.PatchMethod, Is.SameAs(PatchMethod));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "Npgsql" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("NpgsqlCommand"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("ExecuteScalar"));
            Assert.That(definition.TargetParameterTypeNames, Is.EqualTo(new[] { "System.Threading.CancellationToken" }));
        }

        [Test]
        public void Prefix_WithMultipleAssemblyNames_CreatesPrefixDefinition()
        {
            var definition = PatchDefinition.Prefix(
                PatchMethod,
                new[] { "System.Diagnostics.Process", "System" },
                "System.Diagnostics.Process",
                "Start");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Prefix));
            Assert.That(definition.PatchMethod, Is.SameAs(PatchMethod));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "System.Diagnostics.Process", "System" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("System.Diagnostics.Process"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("Start"));
            Assert.That(definition.TargetParameterTypeNames, Is.Empty);
        }

        [Test]
        public void Prefix_WithEmptyAssemblyName_UsesNoAssemblyNames()
        {
            var definition = PatchDefinition.Prefix(
                PatchMethod,
                string.Empty,
                "System.IO.File",
                "ReadAllText",
                "System.String");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Prefix));
            Assert.That(definition.AssemblyNames, Is.Empty);
            Assert.That(definition.TargetParameterTypeNames, Is.EqualTo(new[] { "System.String" }));
        }

        [Test]
        public void Postfix_WithSingleAssemblyName_CreatesPostfixDefinition()
        {
            var definition = PatchDefinition.Postfix(
                PatchMethod,
                "OpenAI",
                "OpenAI.Chat.ChatClient",
                "CompleteChat");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Postfix));
            Assert.That(definition.PatchMethod, Is.SameAs(PatchMethod));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "OpenAI" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("OpenAI.Chat.ChatClient"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("CompleteChat"));
            Assert.That(definition.TargetParameterTypeNames, Is.Empty);
        }

        private static readonly MethodInfo PatchMethod = GetPatchMethod();

        private static MethodInfo GetPatchMethod()
        {
            return typeof(PatchDefinitionTests).GetMethod(
                nameof(TestPatch),
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(PatchDefinitionTests), nameof(TestPatch));
        }

        private static bool TestPatch()
        {
            return true;
        }
    }
}
