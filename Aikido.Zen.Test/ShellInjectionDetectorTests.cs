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
        public void IsShellInjection_ShouldDetectNullByteEndedCommand()
        {
            // Arrange
            string command = "echo whoami\0";
            string userInput = "whoami\0";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when user input contains a null byte ending.");
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

        [Test]
        public void IsShellInjection_ShouldDetectCarriageReturnAsSeparator()
        {
            // Carriage return in user input is flagged
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \rrm", "\rrm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \rrm -rf", "\rrm -rf"), Is.True);

            // Carriage return in user input when user input is the command
            Assert.That(ShellInjectionDetector.IsShellInjection("sleep\r10", "sleep\r10"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("shutdown\r-h\rnow", "shutdown\r-h\rnow"), Is.True);

            // Carriage return as separator between commands
            Assert.That(ShellInjectionDetector.IsShellInjection("ls\rrm", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("echo test\rrm -rf /", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("rm\rls", "rm"), Is.True);
        }

        [Test]
        public void IsShellInjection_ShouldDetectFormFeedAsSeparator()
        {
            // Form feed in user input is flagged
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \frm", "\frm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \frm -rf", "\frm -rf"), Is.True);

            // Form feed in user input when user input is the command
            Assert.That(ShellInjectionDetector.IsShellInjection("sleep\f10", "sleep\f10"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("shutdown\f-h\fnow", "shutdown\f-h\fnow"), Is.True);

            // Form feed as separator between commands
            Assert.That(ShellInjectionDetector.IsShellInjection("ls\frm", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("echo test\frm -rf /", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("rm\fls", "rm"), Is.True);
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
