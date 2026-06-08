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
        private const double DefaultMinimumDeltaNanoseconds = 1000000;

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
                var threshold = double.Parse(Option(args, "--threshold-percent", "35"), CultureInfo.InvariantCulture);
                var minimumDelta = double.Parse(
                    Option(args, "--minimum-delta-ns", DefaultMinimumDeltaNanoseconds.ToString(CultureInfo.InvariantCulture)),
                    CultureInfo.InvariantCulture);
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
                        var delta = currentResult.Mean - baselineResult.Mean;
                        var change = (delta / baselineResult.Mean) * 100;
                        var rangesOverlap = baselineResult.SampleCount > 1 &&
                            currentResult.SampleCount > 1 &&
                            currentResult.Min <= baselineResult.Max;
                        return new ComparisonRow
                        {
                            Display = currentResult.Display,
                            Baseline = baselineResult.Mean,
                            BaselineMin = baselineResult.Min,
                            BaselineMax = baselineResult.Max,
                            Current = currentResult.Mean,
                            CurrentMin = currentResult.Min,
                            CurrentMax = currentResult.Max,
                            Delta = delta,
                            Change = change,
                            Failed = change > threshold && delta > minimumDelta && !rangesOverlap
                        };
                    })
                    .OrderByDescending(row => row.Failed)
                    .ThenByDescending(row => row.Change)
                    .ThenBy(row => row.Display)
                    .ToArray();

                WriteMarkdown(
                    rows,
                    threshold,
                    minimumDelta,
                    current.Keys.Except(baseline.Keys).OrderBy(key => key),
                    baseline.Keys.Except(current.Keys).OrderBy(key => key));

                foreach (var row in rows)
                {
                    var status = row.Failed ? "FAIL" : "PASS";
                    Console.WriteLine(
                        $"{status}: {row.Display} baseline={FormatNanoseconds(row.Baseline)} current={FormatNanoseconds(row.Current)} delta={FormatNanoseconds(row.Delta)} change={Percent(row.Change)}%");
                }

                if (rows.Any(row => row.Failed))
                {
                    Console.Error.WriteLine($"One or more benchmarks regressed by more than {Percent(threshold)}%, {FormatNanoseconds(minimumDelta)}, and outside baseline sample noise.");
                    return 1;
                }

                Console.WriteLine($"Compared {rows.Length} benchmark(s); no regressions over {Percent(threshold)}%, {FormatNanoseconds(minimumDelta)}, and outside baseline sample noise.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
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
                            Mean = mean,
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
            double minimumDeltaNanoseconds,
            IEnumerable<string> missingBaseline,
            IEnumerable<string> missingCurrent)
        {
            var comparisonRows = rows.ToArray();
            var failedCount = comparisonRows.Count(row => row.Failed);
            var builder = new StringBuilder();
            builder.AppendLine("# Benchmark Regression Check");
            builder.AppendLine();
            builder.AppendLine($"Threshold: current mean must not be more than {Percent(thresholdPercent)}% and {FormatNanoseconds(minimumDeltaNanoseconds)} slower than baseline, with no overlap in measured sample ranges.");
            builder.AppendLine($"Compared {comparisonRows.Length} benchmark(s). Regressions: {failedCount}.");
            builder.AppendLine();
            builder.AppendLine("| Benchmark | Baseline mean | Current mean | Delta | Change | Baseline range | Current range | Result |");
            builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

            foreach (var row in comparisonRows)
            {
                builder.AppendLine($"| {Markdown(row.Display)} | {FormatNanoseconds(row.Baseline)} | {FormatNanoseconds(row.Current)} | {FormatNanoseconds(row.Delta)} | {Percent(row.Change)}% | {FormatRange(row.BaselineMin, row.BaselineMax)} | {FormatRange(row.CurrentMin, row.CurrentMax)} | {(row.Failed ? "fail" : "pass")} |");
            }

            AppendMissingBenchmarks(builder, "Benchmarks only present in current results:", missingBaseline);
            AppendMissingBenchmarks(builder, "Benchmarks only present in baseline results:", missingCurrent);
            File.WriteAllText(RegressionReportPath, builder.ToString(), Encoding.UTF8);
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
            public double Mean { get; set; }
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
            public bool Failed { get; set; }
        }
    }
}
