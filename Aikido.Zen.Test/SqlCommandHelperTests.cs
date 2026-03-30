using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test.Helpers
{
    public class SqlCommandHelperTests
    {
        private const string InvalidSqlQuery = "SELECT * FROM users WHERE name = 'abc' OR 1=1 /*";
        private const string InvalidSqlUserInput = "abc' OR 1=1 /*";
        private Context _context;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL", null);
            // mock zen api
            var agentMock = new Agent(ZenApiMock.CreateMock().Object);

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
                Headers = new Dictionary<string, string>
                {
                    { "auth", "token123" }
                },
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.id", "123" },
                    { "body.name", "John" },
                    { "headers.auth", "token123" }
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL", null);
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
        public async Task DetectSQLInjection_WhenInjectionDetected_ReportsDialectMetadata()
        {
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.injection", "1' OR '1'='1" }
            };

            SqlCommandHelper.DetectSQLInjection(
                "SELECT * FROM Users WHERE Id = '1' OR '1'='1'",
                SQLDialect.MicrosoftSQL,
                _context,
                "TestModule",
                "SELECT");

            await Task.Delay(150);

            reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == "sql_injection" &&
                        a.Attack.Path == ".injection" &&
                        a.Attack.Metadata.ContainsKey("sql") &&
                        a.Attack.Metadata.ContainsKey("dialect") &&
                        (string)a.Attack.Metadata["dialect"] == "Microsoft SQL")),
                Times.Once);
        }

        [Test]
        public async Task DetectSQLInjection_WhenInvalidSqlIsBlocked_ReportsFailedToTokenizeMetadata()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL", "true");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.injection", InvalidSqlUserInput }
            };

            var result = SqlCommandHelper.DetectSQLInjection(
                InvalidSqlQuery,
                SQLDialect.Generic,
                _context,
                "TestModule",
                "SELECT");

            await Task.Delay(150);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == "sql_injection" &&
                        a.Attack.Path == ".injection" &&
                        a.Attack.Metadata.ContainsKey("sql") &&
                        a.Attack.Metadata.ContainsKey("dialect") &&
                        a.Attack.Metadata.ContainsKey("failedToTokenize") &&
                        (string)a.Attack.Metadata["failedToTokenize"] == "true")),
                Times.Once);
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
        public void DetectSQLInjection_WhenInvalidSqlBlockingIsDisabled_ShouldNotSendAttackEvent()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL", "false");

            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "query.injection", InvalidSqlUserInput }
            };

            var result = SqlCommandHelper.DetectSQLInjection(
                InvalidSqlQuery,
                SQLDialect.Generic,
                _context,
                "TestModule",
                "SELECT"
            );

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
            string? sqlCommand = null;
            string? moduleName = null;
            string? operation = null;

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
