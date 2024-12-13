using System.Text.Json;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test
{
    public class SQLInjectionDetectorTests
    {
        [TestCaseSource(nameof(GetTestData))]
        public void IsSQLInjection_ShouldDetectInjection(string command, SQLDialect dialect, string userInput, string description, bool expectedResult)
        {
            // Act
            var result = SQLInjectionDetector.IsSQLInjection(command, userInput, dialect);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult), description);
        }

        public static IEnumerable<TestCaseData> GetTestData()
        {
            var jsonData = File.ReadAllText("testdata/data.SQLInjectionDetector.json");
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            foreach (var testCase in testCases)
            {
                yield return new TestCaseData(
                    testCase.Command,
                    testCase.Dialect.ToSQLDialect(),
                    testCase.UserInput,
                    testCase.Description,
                    testCase.IsInjection
                ).SetName($"Test_{(SQLDialect)testCase.Dialect}_{testCase.Description}");
            }
        }

        private class TestCase
        {
            public string Command { get; set; }
            public int Dialect { get; set; }
            public string UserInput { get; set; }
            public string Description { get; set; }
            public bool IsInjection { get; set; }
        }
    }
}
