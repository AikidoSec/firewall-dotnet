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
                SinkKind.SqlClient,
                "Npgsql",
                "NpgsqlCommand",
                "ExecuteScalar",
                "System.Threading.CancellationToken");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Prefix));
            Assert.That(definition.Sink, Is.EqualTo(SinkKind.SqlClient));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "Npgsql" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("NpgsqlCommand"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("ExecuteScalar"));
            Assert.That(definition.TargetParameterTypeNames, Is.EqualTo(new[] { "System.Threading.CancellationToken" }));
        }

        [Test]
        public void Prefix_WithMultipleAssemblyNames_CreatesPrefixDefinition()
        {
            var definition = PatchDefinition.Prefix(
                SinkKind.ProcessExecution,
                new[] { "System.Diagnostics.Process", "System" },
                "System.Diagnostics.Process",
                "Start");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Prefix));
            Assert.That(definition.Sink, Is.EqualTo(SinkKind.ProcessExecution));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "System.Diagnostics.Process", "System" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("System.Diagnostics.Process"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("Start"));
            Assert.That(definition.TargetParameterTypeNames, Is.Empty);
        }

        [Test]
        public void Prefix_WithEmptyAssemblyName_UsesNoAssemblyNames()
        {
            var definition = PatchDefinition.Prefix(
                SinkKind.IOPath,
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
                SinkKind.LLM,
                "OpenAI",
                "OpenAI.Chat.ChatClient",
                "CompleteChat");

            Assert.That(definition.Kind, Is.EqualTo(PatchKind.Postfix));
            Assert.That(definition.Sink, Is.EqualTo(SinkKind.LLM));
            Assert.That(definition.AssemblyNames, Is.EqualTo(new[] { "OpenAI" }));
            Assert.That(definition.TargetTypeName, Is.EqualTo("OpenAI.Chat.ChatClient"));
            Assert.That(definition.TargetMethodName, Is.EqualTo("CompleteChat"));
            Assert.That(definition.TargetParameterTypeNames, Is.Empty);
        }
    }
}
