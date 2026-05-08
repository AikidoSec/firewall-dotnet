using System.Data.Common;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Sinks;
using Moq;

namespace Aikido.Zen.Test
{
    public class SqlClientSinkTests
    {
        private Context _context;
        private MethodInfo _methodInfo;

        [SetUp]
        public void Setup()
        {
            _context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };
            _methodInfo = typeof(DbCommand).GetMethod("ExecuteNonQuery")
                    ?? throw new InvalidOperationException("Could not find DbCommand.ExecuteNonQuery.");

            // setup the agent, because when not running in drymode, SqlClientSink will trigger an attack event
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            var reportingMock = new Mock<IReportingAPIClient>();
            reportingMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>()))
                    .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeMock = new Mock<IRuntimeAPIClient>();
            runtimeMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var zenApiMock = new ZenApi(reportingMock.Object, runtimeMock.Object);

            Agent.NewInstance(zenApiMock);
        }

        [TestCase("System.Data.SqlClient", SQLDialect.MicrosoftSQL)]
        [TestCase("Microsoft.Data.SqlClient", SQLDialect.MicrosoftSQL)]
        [TestCase("System.Data.SqlServerCe", SQLDialect.MicrosoftSQL)]
        [TestCase("Microsoft.Data.Sqlite", SQLDialect.Generic)]
        [TestCase("MySql.Data", SQLDialect.MySQL)]
        [TestCase("Npgsql", SQLDialect.PostgreSQL)]
        [TestCase("MySqlConnector", SQLDialect.MySQL)]
        [TestCase("MySqlX", SQLDialect.MySQL)]
        [TestCase("Unknown.Assembly", SQLDialect.Generic)]
        public void GetDialect_ShouldReturnCorrectDialect(string assembly, SQLDialect expectedDialect)
        {
            // Act
            var result = SqlClientSink.GetDialect(assembly);

            // Assert
            Assert.That(result, Is.EqualTo(expectedDialect));
        }

        [Test]
        public void OnCommandExecuting_WithNullContext_ReturnsTrue()
        {
            // Arrange
            var sql = "SELECT * FROM users";
            var args = new object[] { };

            // Act
            var result = SqlClientSink.OnCommandExecuting(args, _methodInfo, sql, "System.Data.SqlClient", null);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithBypassedContext_ReturnsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.Bypassed = true;
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1'" }
            };
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            var result = SqlClientSink.OnCommandExecuting(
                new object[] { },
                _methodInfo,
                sql,
                "System.Data.SqlClient",
                _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnCommandExecuting_WithSafeQuery_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = @id";
            var args = new object[] { };

            // Act
            var result = SqlClientSink.OnCommandExecuting(args, _methodInfo, sql, "System.Data.SqlClient", _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithNullAssembly_UsesOriginalMethodAssembly()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = @id";

            // Act
            var result = SqlClientSink.OnCommandExecuting(
                Array.Empty<object>(),
                _methodInfo,
                sql,
                null,
                _context);

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
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";
            var args = new object[] { };

            // Act & Assert
            var ex = Assert.Throws<AikidoException>(() =>
                SqlClientSink.OnCommandExecuting(args, _methodInfo, sql, "System.Data.SqlClient", _context)
            );
            Assert.That(ex.Message, Does.Contain("SQL injection detected"));
        }

        [Test]
        public void OnCommandExecuting_WithSQLInjectionInDryMode_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";
            var args = new object[] { };

            // Act
            var result = SqlClientSink.OnCommandExecuting(args, _methodInfo, sql, "System.Data.SqlClient", _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithForceProtectionOffRoute_ReturnsTrueWithoutMarkingAttack()
        {
            _context.Method = "POST";
            _context.Route = "/api/create";
            _context.Path = "/api/create";
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1'" }
            };

            Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "POST",
                    Route = "/api/create",
                    ForceProtectionOff = true
                }
            });

            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            var result = SqlClientSink.OnCommandExecuting(
                new object[] { },
                _methodInfo,
                sql,
                "System.Data.SqlClient",
                _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnCommandExecuting_WithNoCommandArgument_ReturnsTrue()
        {
            Patcher.PatchSinks(() => _context);

            var result = SqlClientSink.OnCommandExecuting(
                Array.Empty<object>(),
                _methodInfo,
                null);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithDbCommandInstance_UsesCommandFromInstance()
        {
            Patcher.PatchSinks(() => _context);
            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");

            var result = SqlClientSink.OnCommandExecuting(
                Array.Empty<object>(),
                _methodInfo,
                dbCommand.Object);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithDbCommandInArgs_UsesCommandFromArgs()
        {
            Patcher.PatchSinks(() => null);
            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");

            var result = SqlClientSink.OnCommandExecuting(
                new object[] { dbCommand.Object },
                _methodInfo,
                null);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithExecuteSqlRawMethodWithoutSqlArgument_ReturnsTrue()
        {
            Patcher.PatchSinks(() => _context);
            var methodInfo = typeof(TestSqlMethods).GetMethod(nameof(TestSqlMethods.ExecuteSqlRaw));

            var result = SqlClientSink.OnCommandExecuting(
                new object[] { new object() },
                methodInfo!,
                null);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithExecuteSqlRawMethod_UsesSqlArgument()
        {
            Patcher.PatchSinks(() => _context);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1'" }
            };
            var methodInfo = typeof(TestSqlMethods).GetMethod(nameof(TestSqlMethods.ExecuteSqlRaw));
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            var ex = Assert.Throws<AikidoException>(() =>
                SqlClientSink.OnCommandExecuting(
                    new object[] { new object(), sql, Array.Empty<object>() },
                    methodInfo!,
                    null));

            Assert.That(ex.Message, Does.Contain("SQL injection detected"));
        }

        [Test]
        public void OnCommandExecuting_WithUnsupportedMethodAndNoDbCommand_ReturnsTrue()
        {
            var methodInfo = typeof(TestSqlMethods).GetMethod(nameof(TestSqlMethods.Query));

            var result = SqlClientSink.OnCommandExecuting(
                new object[] { new object(), "SELECT 1" },
                methodInfo!,
                null);

            Assert.That(result, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_DRY_MODE", null);
        }

        private static class TestSqlMethods
        {
            public static int ExecuteSqlRaw(object databaseFacade, string sql, IEnumerable<object> parameters)
            {
                return 0;
            }

            public static int Query(string sql)
            {
                return 0;
            }
        }
    }
}
