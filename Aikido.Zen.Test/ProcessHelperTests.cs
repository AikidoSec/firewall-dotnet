using System.Diagnostics;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ProcessHelperTests
    {
        [TestCase("", new string[0])]
        [TestCase("   ", new string[0])]
        [TestCase("-c \"echo ok;id\"", new[] { "-c", "echo ok;id" })]
        [TestCase("  a \t \"b c\"  d  ", new[] { "a", "b c", "d" })]
        [TestCase("a\"b c\"d", new[] { "ab cd" })]
        [TestCase("\"\"", new[] { "" })]
        [TestCase("\"\" \"\"", new[] { "", "" })]
        [TestCase("a\"\"b", new[] { "ab" })]
        [TestCase("\"\"\"", new[] { "\"" })]
        [TestCase("\"\"\"\"", new[] { "\"" })]
        [TestCase("\"ab\"cd", new[] { "abcd" })]
        [TestCase("a\nb a\rb", new[] { "a\nb", "a\rb" })]
        [TestCase("\"a b", new[] { "a b" })]
        [TestCase("\"a\\\"b\"", new[] { "a\"b" })]
        [TestCase("\"a\"\"b\"", new[] { "a\"b" })]
        [TestCase("a\\\\b a\\", new[] { "a\\\\b", "a\\" })]
        [TestCase("\"a\\\\\" b", new[] { "a\\", "b" })]
        [TestCase("a\\\\\\\"b", new[] { "a\\\"b" })]
        [TestCase("-c \"echo 'x y'\"", new[] { "-c", "echo 'x y'" })]
        [TestCase("'a b'", new[] { "'a", "b'" })]
        [TestCase("-c \"echo a\";id;\"\"", new[] { "-c", "echo a;id;" })]
        public void DecodeProcessArguments_SplitsLikeProcessStart(string arguments, string[] expected)
        {
            Assert.That(ProcessHelper.DecodeProcessArguments(arguments), Is.EqualTo(expected));
        }

        [Test]
        public void GetArguments_DecodesArgumentsAndPreservesArgumentListValues()
        {
            var expected = new[] { "commit", "-m", "two \"quoted\" words" };

            Assert.That(ProcessHelper.GetArguments(new ProcessStartInfo("git", "commit -m \"two \\\"quoted\\\" words\"")), Is.EqualTo(expected));
            Assert.That(ProcessHelper.GetArguments(new ProcessStartInfo("git", new[] { "commit", "-m", "two \"quoted\" words" })), Is.EqualTo(expected));
            Assert.That(ProcessHelper.GetArguments(new ProcessStartInfo("git")), Is.Empty);
        }

    }
}
