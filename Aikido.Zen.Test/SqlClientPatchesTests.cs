using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class SqlClientPatchesTests
    {
        private Context _context = null!;
        private Agent _agent = null!;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var runtimeApiMock = new Mock<IRuntimeAPIClient>();

            _agent = Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            Patcher.PatchSinks(() => _context);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
        }

        [Test]
        public void PatchMethods_ForwardSqlArgumentsToSink()
        {
            var dbCommand = new Mock<DbCommand>();
            dbCommand.SetupGet(command => command.CommandText).Returns("SELECT 1");
            var dbMethod = GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteScalar));

            Assert.That(SqlClientPatches.OnCommandExecutingDbCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.OnCommandExecutingNPocoCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.OnCommandExecutingDbCommand(null!, dbMethod), Is.True);
            Assert.That(SqlClientPatches.OnCommandExecutingNPocoCommand(null!, dbMethod), Is.True);
            Assert.That(SqlClientPatches.OnCommandExecutingSqlRaw(
                "SELECT 1",
                GetMethod(typeof(TestSqlMethods), nameof(TestSqlMethods.ExecuteSqlRaw), typeof(object), typeof(string), typeof(IEnumerable<object>))), Is.True);
            Assert.That(SqlClientPatches.OnCommandExecutingMySqlXSqlStatement(
                new TestSqlStatement { SQL = "SELECT 1" },
                GetMethod(typeof(TestSqlStatement), nameof(TestSqlStatement.Execute))), Is.True);
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
        [TestCase("", SQLDialect.Generic)]
        public void GetDialect_WithRuntimeInstance_ReturnsCorrectDialect(string assembly, SQLDialect expectedDialect)
        {
            var instance = string.IsNullOrEmpty(assembly)
                ? null
                : CreateInstanceFromAssembly(assembly);

            var result = SqlClientPatches.GetDialect(instance!, null!);

            Assert.That(result, Is.EqualTo(expectedDialect));
        }

        [Test]
        public void GetDialect_WithRuntimeInstance_PrefersInstanceAssembly()
        {
            var instance = CreateInstanceFromAssembly("Npgsql");
            var baseMethod = GetMethod(typeof(DbCommand), nameof(DbCommand.ExecuteNonQueryAsync));

            var result = SqlClientPatches.GetDialect(instance, baseMethod);

            Assert.That(result, Is.EqualTo(SQLDialect.PostgreSQL));
        }

        [Test]
        public void MySqlXPatchTargets_RawSqlExecuteOnly()
        {
            var sqlStatementMethod = GetMethod(
                typeof(SqlClientPatches),
                nameof(SqlClientPatches.OnCommandExecutingMySqlXSqlStatement),
                typeof(object),
                typeof(MethodBase));

            var sqlStatementTargets = sqlStatementMethod.GetCustomAttributes<SinkPrefixAttribute>().ToArray();

            Assert.That(sqlStatementTargets, Has.Length.EqualTo(1));
            Assert.That(sqlStatementTargets[0].AssemblyName, Is.EqualTo("MySql.Data"));
            Assert.That(sqlStatementTargets[0].TargetTypeName, Is.EqualTo("MySqlX.XDevAPI.Relational.SqlStatement"));
            Assert.That(sqlStatementTargets[0].TargetMethodName, Is.EqualTo("Execute"));
        }

        [Test]
        public void MySqlXSqlStatement_WithInterpolatedInput_Throws()
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
                SqlClientPatches.OnCommandExecutingMySqlXSqlStatement(
                    statement,
                    GetMethod(typeof(TestSqlStatement), nameof(TestSqlStatement.Execute))));

            Assert.That(ex!.Message, Does.Contain("SQL injection detected"));
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

        private static object CreateInstanceFromAssembly(string assemblyName)
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(assemblyName),
                AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule($"{assemblyName}.Module");
            var typeBuilder = moduleBuilder.DefineType(
                $"{assemblyName}.RuntimeInstance",
                TypeAttributes.Public);

            return Activator.CreateInstance(typeBuilder.CreateTypeInfo()!.AsType())!;
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
    }
}
