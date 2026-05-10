using System.Data.Common;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
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

            Assert.That(SqlClientPatches.DbCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.NPocoCommand(dbCommand.Object, dbMethod), Is.True);
            Assert.That(SqlClientPatches.ExecuteSqlRaw(
                "SELECT 1",
                GetMethod(typeof(TestSqlMethods), nameof(TestSqlMethods.ExecuteSqlRaw), typeof(object), typeof(string), typeof(IEnumerable<object>))), Is.True);
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

        private static class TestSqlMethods
        {
            public static int ExecuteSqlRaw(object databaseFacade, string sql, IEnumerable<object> parameters)
            {
                return 0;
            }
        }
    }
}
