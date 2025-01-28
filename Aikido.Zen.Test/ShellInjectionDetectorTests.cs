using System.Text.Json;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class ShellInjectionDetectorTests
    {
        [TestCaseSource(nameof(GetTestData))]
        public void IsShellInjection_ShouldDetectInjection(string command, string userInput, string description, bool expectedResult)
        {
            // Arrange

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult), description);
        }

        [Test]
        public void IsSafelyEncapsulated_Tests()
        {
            // Test: safe between single quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '`$USER'", "`USER"), Is.True);

            // Test: single quote in single quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo ''USER'", "'USER"), Is.False);

            // Test: dangerous chars between double quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"=USER\"", "=USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\"", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"!USER\"", "!USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"`USER\"", "`USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"\\USER\"", "\\USER"), Is.False);

            // Test: same user input multiple times
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER' '$USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\" '$USER'", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\" \"$USER\"", "$USER"), Is.False);

            // Test: the first and last quote doesn't match
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER\"", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER'", "$USER"), Is.False);

            // Test: the first or last character is not an escape char
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo $USER'", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo $USER\"", "$USER"), Is.False);

            // Test: user input does not occur in the command
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo 'USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"USER\"", "$USER"), Is.True);
        }

        public static IEnumerable<TestCaseData> GetTestData()
        {
            var jsonData = File.ReadAllText("testdata/data.ShellInjectionDetector.json");
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            foreach (var testCase in testCases)
            {
                yield return new TestCaseData(
                    testCase.Command,
                    testCase.UserInput,
                    testCase.Description,
                    testCase.IsInjection
                ).SetName($"Test_{testCase.Description}");
            }
        }

        private class TestCase
        {
            public string Command { get; set; }
            public string UserInput { get; set; }
            public string Description { get; set; }
            public bool IsInjection { get; set; }
        }
    }
}
