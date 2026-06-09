using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using Aikido.Zen.Core.Vulnerabilities;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class SQLInjectionDetectionBenchmarks
    {
        private const int DetectOperationsPerInvocation = 2_000_000;
        private const int LongQueryOperationsPerInvocation = 500_000;
        private const int LongUserInputOperationsPerInvocation = 1_000_000;
        private const int SafeInputOperationsPerInvocation = 2_500_000;

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
        public int DetectSQLInjection()
        {
            var detected = 0;
            for (int i = 0; i < DetectOperationsPerInvocation; i++)
            {
                detected += SQLInjectionDetector.IsSQLInjection(_query, _userInput, Dialect) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectSQLInjectionWithLongQuery()
        {
            var detected = 0;
            for (int i = 0; i < LongQueryOperationsPerInvocation; i++)
            {
                detected += SQLInjectionDetector.IsSQLInjection(_longQuery, _userInput, Dialect) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectSQLInjectionWithLongUserInput()
        {
            var detected = 0;
            for (int i = 0; i < LongUserInputOperationsPerInvocation; i++)
            {
                detected += SQLInjectionDetector.IsSQLInjection(_query, _longUserInput, Dialect) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectSQLInjectionWithSafeInput()
        {
            var detected = 0;
            for (int i = 0; i < SafeInputOperationsPerInvocation; i++)
            {
                detected += SQLInjectionDetector.IsSQLInjection(_query, "123", Dialect) ? 1 : 0;
            }

            return detected;
        }
    }
}
