using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using Aikido.Zen.Core.Vulnerabilities;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class SQLInjectionDetectionBenchmarks
    {
        private string _query;
        private string _userInput;

        [Params(SQLDialect.MySQL, SQLDialect.PostgreSQL, SQLDialect.Generic)]
        public SQLDialect Dialect { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _query = "SELECT * FROM users WHERE id = @id AND name LIKE @name";
            _userInput = "1'; DROP TABLE users; --";
        }

        [Benchmark]
        public bool DetectSQLInjection()
        {
            return SQLInjectionDetector.IsSQLInjection(_query, _userInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithLongQuery()
        {
            var longQuery = _query;
            for (int i = 0; i < 10; i++)
            {
                longQuery += " UNION " + _query;
            }
            return SQLInjectionDetector.IsSQLInjection(longQuery, _userInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithLongUserInput()
        {
            var longUserInput = _userInput;
            for (int i = 0; i < 10; i++)
            {
                longUserInput += " OR " + _userInput;
            }
            return SQLInjectionDetector.IsSQLInjection(_query, longUserInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithSafeInput()
        {
            return SQLInjectionDetector.IsSQLInjection(_query, "123", Dialect);
        }
    }
}
