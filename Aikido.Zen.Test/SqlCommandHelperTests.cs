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
    }
}
