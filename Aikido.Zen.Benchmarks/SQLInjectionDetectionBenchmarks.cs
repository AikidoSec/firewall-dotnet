using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using Aikido.Zen.Core.Vulnerabilities;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class SQLInjectionDetectionBenchmarks
    {
        private string _query;
        private string _userInput;
        private string _longQuery;
        private string _longUserInput;

        [Params(SQLDialect.MySQL, SQLDialect.PostgreSQL, SQLDialect.Generic)]
        public SQLDialect Dialect { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _query = "SELECT * FROM users WHERE id = @id AND name LIKE @name";
            _userInput = "1'; DROP TABLE users; --";
            _longQuery = _query;
            _longUserInput = _userInput;

            for (int i = 0; i < 10; i++)
            {
                _longQuery += " UNION " + _query;
                _longUserInput += " OR " + _userInput;
            }
        }

        [Benchmark]
        public bool DetectSQLInjection()
        {
            return SQLInjectionDetector.IsSQLInjection(_query, _userInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithLongQuery()
        {
            return SQLInjectionDetector.IsSQLInjection(_longQuery, _userInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithLongUserInput()
        {
            return SQLInjectionDetector.IsSQLInjection(_query, _longUserInput, Dialect);
        }

        [Benchmark]
        public bool DetectSQLInjectionWithSafeInput()
        {
            return SQLInjectionDetector.IsSQLInjection(_query, "123", Dialect);
        }
    }
}
