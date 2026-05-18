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
        private Context? _activeContext;

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
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
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
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [Test]
        public void OnCommandExecuting_WithNullContext_ReturnsTrue()
        {
            // Arrange
            var sql = "SELECT * FROM users";

            // Act
            var result = OnCommandExecuting(_methodInfo, sql, null);

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

            var result = OnCommandExecuting(_methodInfo, sql, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnCommandExecuting_WithSafeQuery_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = @id";

            // Act
            var result = OnCommandExecuting(_methodInfo, sql, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithExplicitDialect_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = @id";

            // Act
            var result = OnCommandExecuting(_methodInfo, sql, _context);

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

            // Act & Assert
            var ex = Assert.Throws<AikidoException>(() =>
                OnCommandExecuting(_methodInfo, sql, _context)
            );
            Assert.That(ex.Message, Does.Contain("Zen has blocked an SQL injection"));
        }

        [Test]
        public void OnCommandExecuting_WithSQLInjectionInDryMode_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            // Act
            var result = OnCommandExecuting(_methodInfo, sql, _context);

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

            var result = OnCommandExecuting(_methodInfo, sql, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnCommandExecuting_WithMissingUserInputCollection_ReturnsTrue()
        {
#pragma warning disable CS8625
            _context.ParsedUserInput = null;
#pragma warning restore CS8625
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            var result = OnCommandExecuting(_methodInfo, sql, _context);

            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void OnCommandExecuting_WithNullSql_ReturnsTrue()
        {
            var result = OnCommandExecuting(_methodInfo, null, _context);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithCommandText_ReturnsTrue()
        {
            var result = OnCommandExecuting(_methodInfo, "SELECT 1", _context);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithExecuteSqlRawMethodAndNullSql_ReturnsTrue()
        {
            var methodInfo = typeof(TestSqlMethods).GetMethod(nameof(TestSqlMethods.ExecuteSqlRaw));

            var result = OnCommandExecutingSqlRaw(methodInfo!, null, _context);

            Assert.That(result, Is.True);
        }

        [Test]
        public void OnCommandExecuting_WithExecuteSqlRawMethod_UsesSqlArgument()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1'" }
            };
            var methodInfo = typeof(TestSqlMethods).GetMethod(nameof(TestSqlMethods.ExecuteSqlRaw));
            var sql = "SELECT * FROM users WHERE id = '1' OR '1'='1'";

            var ex = Assert.Throws<AikidoException>(() =>
                OnCommandExecutingSqlRaw(methodInfo!, sql, _context));

            Assert.That(ex.Message, Does.Contain("Zen has blocked an SQL injection"));
        }

        [Test]
        public void OnCommandExecuting_NPocoAndMySqlXEntryPoints_ReturnTrueForSafeSql()
        {
            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");
            var dbMethod = GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalar));

            Assert.That(OnNPocoCommandExecuting(dbMethod, dbCommand.Object, _context), Is.True);
            Assert.That(OnNPocoCommandExecuting(dbMethod, null, _context), Is.True);
            Assert.That(OnMySqlXSqlStatement(
                GetMethod(typeof(TestSqlStatement), nameof(TestSqlStatement.Execute)),
                new TestSqlStatement { SQL = "SELECT 1" },
                _context), Is.True);
        }

        [Test]
        public void OnCommandExecuting_MySqlXPatchTargetsRawSqlExecuteOnly()
        {
            var sqlStatementMethod = GetMethod(
                typeof(SqlClientSink),
                nameof(SqlClientSink.OnCommandExecutingMySqlXSqlStatement),
                typeof(object),
                typeof(MethodBase));

            var sqlStatementTargets = sqlStatementMethod.GetCustomAttributes<SinkPrefixAttribute>().ToArray();

            Assert.That(sqlStatementTargets, Has.Length.EqualTo(1));
            Assert.That(sqlStatementTargets[0].AssemblyName, Is.EqualTo("MySql.Data"));
            Assert.That(sqlStatementTargets[0].TargetTypeName, Is.EqualTo("MySqlX.XDevAPI.Relational.SqlStatement"));
            Assert.That(sqlStatementTargets[0].TargetMethodName, Is.EqualTo("Execute"));
        }

        [Test]
        public void OnCommandExecuting_MySqlXSqlStatementWithInterpolatedInput_Throws()
        {
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "body.query", "1' OR '1'='1" }
            };

            var statement = new TestSqlStatement
            {
                SQL = "SELECT * FROM users WHERE id = '1' OR '1'='1'"
            };

            var ex = Assert.Throws<AikidoException>(() =>
                OnMySqlXSqlStatement(
                    GetMethod(typeof(TestSqlStatement), nameof(TestSqlStatement.Execute)),
                    statement,
                    _context));

            Assert.That(ex!.Message, Does.Contain("Zen has blocked an SQL injection"));
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

        }

        private sealed class TestSqlStatement
        {
            public string SQL { get; set; } = string.Empty;

            public object Execute()
            {
                return new object();
            }
        }

        private bool OnCommandExecuting(MethodInfo methodInfo, string? sql, Context? context)
        {
            _activeContext = context;
            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns(sql);
            return SqlClientSink.OnCommandExecutingDbCommand(dbCommand.Object, methodInfo);
        }

        private bool OnCommandExecutingSqlRaw(MethodInfo methodInfo, string? sql, Context? context)
        {
            _activeContext = context;
            return SqlClientSink.OnCommandExecutingSqlRaw(sql!, methodInfo);
        }

        private bool OnNPocoCommandExecuting(MethodInfo methodInfo, DbCommand? dbCommand, Context? context)
        {
            _activeContext = context;
            return SqlClientSink.OnCommandExecutingNPocoCommand(dbCommand!, methodInfo);
        }

        private bool OnMySqlXSqlStatement(MethodInfo methodInfo, object statement, Context? context)
        {
            _activeContext = context;
            return SqlClientSink.OnCommandExecutingMySqlXSqlStatement(statement, methodInfo);
        }

        private static MethodInfo GetMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }
    }
}
