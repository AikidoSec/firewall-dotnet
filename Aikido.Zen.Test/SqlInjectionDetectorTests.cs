using System.Text.Json;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

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

        [Test]
        public void IsSQLInjection_WithNullQuery_ReturnsFalse()
        {
            // Act
            var result = SQLInjectionDetector.IsSQLInjection(null, "input", SQLDialect.MySQL);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSQLInjection_WithNullUserInput_ReturnsFalse()
        {
            // Act
            var result = SQLInjectionDetector.IsSQLInjection("SELECT * FROM users", null, SQLDialect.MySQL);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSQLInjection_WithEmptyQuery_ReturnsFalse()
        {
            // Act
            var result = SQLInjectionDetector.IsSQLInjection("", "input", SQLDialect.MySQL);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSQLInjection_WithEmptyUserInput_ReturnsFalse()
        {
            // Act
            var result = SQLInjectionDetector.IsSQLInjection("SELECT * FROM users", "", SQLDialect.MySQL);

            // Assert
            Assert.That(result, Is.False);
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
