using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Aikido.Zen.Benchmarks
{
    internal class Program
    {
        private const string BaselineResultsDirectory = "baseline-results/results";
        private const string CurrentResultsDirectory = "BenchmarkDotNet.Artifacts/results";
        private const string RegressionReportPath = "benchmark-regression.md";

        static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--compare-results", StringComparison.OrdinalIgnoreCase))
            {
                return CompareResults(args.Skip(1).ToArray());
            }

            var benchmarkArgs = args.Length == 0 ? new[] { "--filter", "*" } : args;
            var config = ManualConfig
                .Create(DefaultConfig.Instance)
                .AddExporter(MarkdownExporter.Console)
                .AddExporter(JsonExporter.Full);

            SetBenchmarkEnvironmentDefaults();

            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(benchmarkArgs, config);

            return 0;
        }

        private static int CompareResults(string[] args)
        {
            try
            {
                var baseline = LoadResults(BaselineResultsDirectory);
                var current = LoadResults(CurrentResultsDirectory);
                var threshold = double.Parse(Option(args, "--threshold-percent", "20"), CultureInfo.InvariantCulture);
                var matchingKeys = baseline.Keys.Intersect(current.Keys).OrderBy(key => key).ToArray();

                if (matchingKeys.Length == 0)
                {
                    Console.Error.WriteLine("No comparable benchmark results were found.");
                    return 1;
                }

                var rows = matchingKeys
                    .Select(key =>
                    {
                        var baselineResult = baseline[key];
                        var currentResult = current[key];
                        var delta = currentResult.Median - baselineResult.Median;
                        var change = (delta / baselineResult.Median) * 100;
                        var rangesOverlap = baselineResult.SampleCount > 1 &&
                            currentResult.SampleCount > 1 &&
                            baselineResult.Min <= currentResult.Max &&
                            currentResult.Min <= baselineResult.Max;
                        var failed = change > threshold && !rangesOverlap;
                        return new ComparisonRow
                        {
                            Display = currentResult.Display,
                            Baseline = baselineResult.Median,
                            BaselineMin = baselineResult.Min,
                            BaselineMax = baselineResult.Max,
                            Current = currentResult.Median,
                            CurrentMin = currentResult.Min,
                            CurrentMax = currentResult.Max,
                            Delta = delta,
                            Change = change,
                            Outcome = Outcome(failed, rangesOverlap)
                        };
                    })
                    .OrderBy(row => row.OutcomeRank)
                    .ThenByDescending(row => Math.Abs(row.Change))
                    .ThenBy(row => row.Display)
                    .ToArray();

                WriteMarkdown(
                    rows,
                    threshold,
                    current.Keys.Except(baseline.Keys).OrderBy(key => key),
                    baseline.Keys.Except(current.Keys).OrderBy(key => key));

                foreach (var row in rows)
                {
                    Console.WriteLine(
                        $"{OutcomeText(row.Outcome)}: {row.Display} baseline={FormatNanoseconds(row.Baseline)} current={FormatNanoseconds(row.Current)} delta={FormatNanoseconds(row.Delta)} change={Percent(row.Change)}%");
                }

                if (rows.Any(row => row.Outcome == ComparisonOutcome.Fail))
                {
                    Console.Error.WriteLine($"One or more benchmarks regressed by more than {Percent(threshold)}% without sample overlap.");
                    return 1;
                }

                Console.WriteLine($"Compared {rows.Length} benchmark(s); no regressions over {Percent(threshold)}% without sample overlap.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static void SetBenchmarkEnvironmentDefaults()
        {
            SetEnvironmentDefault("DOTNET_TieredCompilation", "0");
            SetEnvironmentDefault("DOTNET_TieredPGO", "0");
            SetEnvironmentDefault("DOTNET_ReadyToRun", "0");
            SetEnvironmentDefault("COMPlus_TieredCompilation", "0");
            SetEnvironmentDefault("COMPlus_TieredPGO", "0");
            SetEnvironmentDefault("COMPlus_ReadyToRun", "0");
        }

        private static void SetEnvironmentDefault(string name, string value)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private static Dictionary<string, BenchmarkResult> LoadResults(string resultsDirectory)
        {
            var results = new Dictionary<string, BenchmarkResult>();
            foreach (var path in Directory.EnumerateFiles(resultsDirectory, "*-full.json", SearchOption.AllDirectories).OrderBy(path => path))
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarks) ||
                        benchmarks.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var benchmark in benchmarks.EnumerateArray())
                    {
                        if (!benchmark.TryGetProperty("Statistics", out var statistics) ||
                            statistics.ValueKind != JsonValueKind.Object ||
                            !statistics.TryGetProperty("Mean", out var meanElement) ||
                            meanElement.ValueKind != JsonValueKind.Number)
                        {
                            continue;
                        }

                        var mean = meanElement.GetDouble();
                        var median = statistics.TryGetProperty("Median", out var medianElement) && medianElement.ValueKind == JsonValueKind.Number
                            ? medianElement.GetDouble()
                            : mean;
                        var min = statistics.TryGetProperty("Min", out var minElement) && minElement.ValueKind == JsonValueKind.Number
                            ? minElement.GetDouble()
                            : mean;
                        var max = statistics.TryGetProperty("Max", out var maxElement) && maxElement.ValueKind == JsonValueKind.Number
                            ? maxElement.GetDouble()
                            : mean;
                        var sampleCount = statistics.TryGetProperty("N", out var sampleCountElement) && sampleCountElement.ValueKind == JsonValueKind.Number
                            ? sampleCountElement.GetInt32()
                            : 1;
                        var key = BenchmarkKey(benchmark);
                        results[key] = new BenchmarkResult
                        {
                            Median = median,
                            Min = min,
                            Max = max,
                            SampleCount = sampleCount,
                            Display = benchmark.GetProperty("DisplayInfo").GetString()
                        };
                    }
                }
            }

            return results;
        }

        private static ComparisonOutcome Outcome(bool failed, bool rangesOverlap)
        {
            if (failed)
            {
                return ComparisonOutcome.Fail;
            }

            if (rangesOverlap)
            {
                return ComparisonOutcome.OverlappingRange;
            }

            return ComparisonOutcome.Pass;
        }

        private static string BenchmarkKey(JsonElement benchmark)
        {
            var fullName = benchmark.GetProperty("FullName").GetString();
            var parameters = JsonText(benchmark.GetProperty("Parameters"));
            return string.IsNullOrEmpty(parameters) ? fullName : $"{fullName} | {parameters}";
        }

        private static string JsonText(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }

        private static string Option(string[] args, string name, string defaultValue)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {name}.");
                }

                return args[i + 1];
            }

            return defaultValue;
        }

        private static void WriteMarkdown(
            IEnumerable<ComparisonRow> rows,
            double thresholdPercent,
            IEnumerable<string> missingBaseline,
            IEnumerable<string> missingCurrent)
        {
            var comparisonRows = rows.ToArray();
            var failedCount = comparisonRows.Count(row => row.Outcome == ComparisonOutcome.Fail);
            var passCount = comparisonRows.Count(row => row.Outcome == ComparisonOutcome.Pass);
            var overlappingRangeCount = comparisonRows.Count(row => row.Outcome == ComparisonOutcome.OverlappingRange);
            var builder = new StringBuilder();
            builder.AppendLine("# Benchmark Regression Check");
            builder.AppendLine();
            builder.AppendLine($"Threshold: current median must not be more than {Percent(thresholdPercent)}% slower than baseline unless baseline/current samples overlap.");
            builder.AppendLine("Sample overlap means the baseline min-to-max measurement range intersects the current min-to-max measurement range, so the median change is still within observed measurement variance.");
            builder.AppendLine($"Compared {comparisonRows.Length} benchmark(s). Regressions: {failedCount}. Pass: {passCount}. Sample overlap: {overlappingRangeCount}.");
            builder.AppendLine();
            AppendComparisonTable(builder, comparisonRows);

            AppendMissingBenchmarks(builder, "Benchmarks only present in current results:", missingBaseline);
            AppendMissingBenchmarks(builder, "Benchmarks only present in baseline results:", missingCurrent);
            File.WriteAllText(RegressionReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendComparisonTable(StringBuilder builder, IEnumerable<ComparisonRow> rows)
        {
            builder.AppendLine("| Benchmark | Baseline median | Current median | Delta | Change | Baseline range | Current range | Result |");
            builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {Markdown(row.Display)} | {FormatNanoseconds(row.Baseline)} | {FormatNanoseconds(row.Current)} | {FormatNanoseconds(row.Delta)} | {Percent(row.Change)}% | {FormatRange(row.BaselineMin, row.BaselineMax)} | {FormatRange(row.CurrentMin, row.CurrentMax)} | {OutcomeText(row.Outcome)} |");
            }
        }

        private static string OutcomeText(ComparisonOutcome outcome)
        {
            switch (outcome)
            {
                case ComparisonOutcome.Fail:
                    return "fail";
                case ComparisonOutcome.OverlappingRange:
                    return "sample overlap";
                case ComparisonOutcome.Pass:
                    return "pass";
                default:
                    return "pass";
            }
        }

        private static void AppendMissingBenchmarks(StringBuilder builder, string title, IEnumerable<string> keys)
        {
            var missingKeys = keys.ToArray();
            if (missingKeys.Length == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine(title);
            foreach (var key in missingKeys)
            {
                builder.AppendLine($"- `{key}`");
            }
        }

        private static string Markdown(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
        }

        private static string Percent(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string FormatNanoseconds(double value)
        {
            var sign = value < 0 ? "-" : string.Empty;
            var absoluteValue = Math.Abs(value);

            if (absoluteValue >= 1000000)
            {
                return $"{sign}{(absoluteValue / 1000000).ToString("F3", CultureInfo.InvariantCulture)} ms";
            }

            if (absoluteValue >= 1000)
            {
                return $"{sign}{(absoluteValue / 1000).ToString("F3", CultureInfo.InvariantCulture)} us";
            }

            return $"{sign}{absoluteValue.ToString("F3", CultureInfo.InvariantCulture)} ns";
        }

        private static string FormatRange(double min, double max)
        {
            return $"{FormatNanoseconds(min)} - {FormatNanoseconds(max)}";
        }

        private sealed class BenchmarkResult
        {
            public double Median { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public int SampleCount { get; set; }
            public string Display { get; set; }
        }

        private sealed class ComparisonRow
        {
            public string Display { get; set; }
            public double Baseline { get; set; }
            public double BaselineMin { get; set; }
            public double BaselineMax { get; set; }
            public double Current { get; set; }
            public double CurrentMin { get; set; }
            public double CurrentMax { get; set; }
            public double Delta { get; set; }
            public double Change { get; set; }
            public ComparisonOutcome Outcome { get; set; }
            public int OutcomeRank
            {
                get
                {
                    switch (Outcome)
                    {
                        case ComparisonOutcome.Fail:
                            return 0;
                        case ComparisonOutcome.Pass:
                            return 1;
                        case ComparisonOutcome.OverlappingRange:
                            return 2;
                        default:
                            return 3;
                    }
                }
            }
        }

        private enum ComparisonOutcome
        {
            Fail,
            Pass,
            OverlappingRange
        }
    }
}
