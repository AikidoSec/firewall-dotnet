using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Moq;

namespace Aikido.Zen.Test
{
    public class SqlCommandHelperTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test");
            // mock zen api
            var reportingApiClient = new Mock<IReportingAPIClient>();
            reportingApiClient
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), 5000))
                .ReturnsAsync(new ReportingAPIResponse());
            var runtimeApiClient = new Mock<IRuntimeAPIClient>();
            var zenApi = new Mock<IZenApi>();
            zenApi.Setup(z => z.Reporting).Returns(reportingApiClient.Object);
            zenApi.Setup(z => z.Runtime).Returns(runtimeApiClient.Object);
            Agent.GetInstance(zenApi.Object);
            // mock the static GetInstance method

            // Setup context with some parsed user input
            _context = new Context
            {
                Body = new MemoryStream("test".Select(c => (byte)c).ToArray()),
                Url = "https://example.com/test",
                Method = "GET",
                Cookies = new Dictionary<string, string>
                {
                    { "session", "123" }
                },
                Headers = new Dictionary<string, string[]>
                {
                    { "auth", ["token123"] }
                },
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.id", "123" },
                    { "body.name", "John" },
                    { "headers.auth", "token123" }
                }
            };
        }

        [Test]
        public void DetectSQLInjection_WhenInjectionDetected_ShouldSendAttackEvent()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM Users WHERE Id = '1' OR '1'='1'";
            string moduleName = "TestModule";
            string operation = "SELECT";

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.injection", "1' OR '1'='1" }
            };

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand, 
                SQLDialect.MicrosoftSQL, 
                _context, 
                moduleName, 
                operation
            );

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectSQLInjection_WithSafeInput_ShouldNotSendAttackEvent()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM Users WHERE Id = @Id";
            string moduleName = "TestModule";
            string operation = "SELECT";

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.id", "123" }
            };

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand, 
                SQLDialect.MicrosoftSQL, 
                _context, 
                moduleName, 
                operation
            );

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectSQLInjection_WithEmptyContext_ShouldReturnFalse()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM Users";
            var emptyContext = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                emptyContext,
                "TestModule",
                "SELECT"
            );

            // Assert
            Assert.That(result, Is.False);
            Assert.That(emptyContext.AttackDetected, Is.False);
        }

        [Test]
        public void DetectSQLInjection_WithNullValues_ShouldHandleGracefully()
        {
            // Arrange
            string sqlCommand = null;
            string moduleName = null;
            string operation = null;

            // Act & Assert
            Assert.DoesNotThrow(() => SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                _context,
                moduleName,
                operation
            ));
        }

        [Test]
        public void DetectSQLInjection_WithDifferentSQLCommandTypes_ShouldHandleCorrectly()
        {
            // Arrange
            string[] sqlCommands = {
                "INSERT INTO Users (Name) VALUES ('John')",
                "UPDATE Users SET Name = 'Jane' WHERE Id = 1",
                "DELETE FROM Users WHERE Id = 1"
            };
            string moduleName = "TestModule";
            string operation = "MODIFY";

            foreach (var sqlCommand in sqlCommands)
            {
                // Act
                bool result = SqlCommandHelper.DetectSQLInjection(
                    sqlCommand,
                    SQLDialect.MicrosoftSQL,
                    _context,
                    moduleName,
                    operation
                );

                // Assert
                Assert.That(result, Is.False);
                Assert.That(_context.AttackDetected, Is.False);
            }
        }

        [Test]
        public void DetectSQLInjection_WithParameterHandling_ShouldNotSendAttackEvent()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM Users WHERE Id = @Id AND Name = @Name";
            string moduleName = "TestModule";
            string operation = "SELECT";

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.id", "123" },
                { "query.name", "John" }
            };

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                _context,
                moduleName,
                operation
            );

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectSQLInjection_WithErrorScenarios_ShouldHandleGracefully()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM NonExistentTable";
            string moduleName = "TestModule";
            string operation = "SELECT";

            // Act & Assert
            Assert.DoesNotThrow(() => SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                _context,
                moduleName,
                operation
            ));
        }

        [Test]
        public void DetectSQLInjection_WithTransactionHandling_ShouldNotSendAttackEvent()
        {
            // Arrange
            string sqlCommand = "BEGIN TRANSACTION; SELECT * FROM Users; COMMIT;";
            string moduleName = "TestModule";
            string operation = "TRANSACTION";

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                _context,
                moduleName,
                operation
            );

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectSQLInjection_WithConnectionManagement_ShouldNotSendAttackEvent()
        {
            // Arrange
            string sqlCommand = "SELECT * FROM Users; -- Connection management test";
            string moduleName = "TestModule";
            string operation = "SELECT";

            // Act
            bool result = SqlCommandHelper.DetectSQLInjection(
                sqlCommand,
                SQLDialect.MicrosoftSQL,
                _context,
                moduleName,
                operation
            );

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }
    }
}
