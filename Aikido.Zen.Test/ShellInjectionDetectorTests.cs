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

        [Test]
        public void IsShellInjection_ShouldDetectTildeInjection()
        {
            // Arrange
            string command = "echo ~user";
            string userInput = "~";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect tilde injection when userInput is '~' and command contains '~'.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectSeparatorBeforeAndAfter()
        {
            // Arrange
            string command = "echo whoami";
            string userInput = "whoami";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when separators are before and after the user input.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectSeparatorBeforeEndOfString()
        {
            // Arrange
            string command = "echo $USER";
            string userInput = "echo";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when separator is before and end of string after user input.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectStartOfStringSeparatorAfter()
        {
            // Arrange
            string command = "$USER echo";
            string userInput = "$USER";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when start of string and separator is after user input.");
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
