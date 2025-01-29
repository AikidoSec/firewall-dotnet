using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Patches;
using Moq;
using System.Data.Common;
using System.Reflection;

namespace Aikido.Zen.Test
{
    public class SqlClientPatcherTests
    {
        private Mock<DbCommand> _commandMock;
        private Context _context;
        private MethodInfo _methodInfo;

        [SetUp]
        public void Setup()
        {
            _commandMock = new Mock<DbCommand>();
            _context = new Context();
            _methodInfo = typeof(DbCommand).GetMethod("ExecuteNonQuery");
            // setup the agent, because when not running in drymode, SqlClientPatcher will trigger an attack event
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            var reportingMock = new Mock<IReportingAPIClient>();
            reportingMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()))
                    .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeMock = new Mock<IRuntimeAPIClient>();
            runtimeMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var zenApiMock = new ZenApi(reportingMock.Object, runtimeMock.Object);

            Agent.NewInstance(zenApiMock);
        }

        [TestCase("System.Data.SqlClient", SQLDialect.MicrosoftSQL)]
        [TestCase("Microsoft.Data.Sqlite", SQLDialect.Generic)]
        [TestCase("MySql.Data", SQLDialect.MySQL)]
        [TestCase("Npgsql", SQLDialect.PostgreSQL)]
        [TestCase("MySqlConnector", SQLDialect.MySQL)]
        [TestCase("Unknown.Assembly", SQLDialect.Generic)]
        public void GetDialect_ShouldReturnCorrectDialect(string assembly, SQLDialect expectedDialect)
        {
            // Act
            var result = SqlClientPatcher.GetDialect(assembly);

            // Assert
            Assert.That(result, Is.EqualTo(expectedDialect));
        }

        [Test]
        public void OnCommandExecuting_WithNullContext_ReturnsTrue()
        {
            // Arrange
            _commandMock.Setup(c => c.CommandText).Returns("SELECT * FROM users");
            var args = new object[] { };

            // Act
            var result = SqlClientPatcher.OnCommandExecuting(args, _methodInfo, _commandMock.Object, "System.Data.SqlClient", null);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithSafeQuery_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _commandMock.Setup(c => c.CommandText).Returns("SELECT * FROM users WHERE id = @id");
            var args = new object[] { };

            // Act
            var result = SqlClientPatcher.OnCommandExecuting(args, _methodInfo, _commandMock.Object, "System.Data.SqlClient", _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithSQLInjection_ThrowsException()
        {
            // Arrange
            _context.ParsedUserInput = new Dictionary<string, string> {
             { "body.query", "1' OR '1'='1'" }
            };
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _commandMock.Setup(c => c.CommandText).Returns("SELECT * FROM users WHERE id = '1' OR '1'='1'");
            var args = new object[] { };

            // Act & Assert
            var ex = Assert.Throws<AikidoException>(() =>
                SqlClientPatcher.OnCommandExecuting(args, _methodInfo, _commandMock.Object, "System.Data.SqlClient", _context)
            );
            Assert.That(ex.Message, Does.Contain("SQL injection detected"));
        }

        [Test]
        public void OnCommandExecuting_WithSQLInjectionInDryMode_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _commandMock.Setup(c => c.CommandText).Returns("SELECT * FROM users WHERE id = '1' OR '1'='1'");
            var args = new object[] { };

            // Act
            var result = SqlClientPatcher.OnCommandExecuting(args, _methodInfo, _commandMock.Object, "System.Data.SqlClient", _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DRY_MODE", null);
        }
    }
}
