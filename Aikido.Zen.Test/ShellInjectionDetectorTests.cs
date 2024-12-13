using System.Text.Json;
using Aikido.Zen.Core.Vulnerabilities;

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
