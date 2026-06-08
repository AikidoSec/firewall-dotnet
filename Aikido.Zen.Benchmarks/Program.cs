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
                var baseline = LoadResults(RequiredOption(args, "--baseline"));
                var current = LoadResults(RequiredOption(args, "--current"));
                var threshold = double.Parse(Option(args, "--threshold-percent", "15"), CultureInfo.InvariantCulture);
                var outputMarkdown = Option(args, "--output-md", "benchmark-regression.md");
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
                        var change = ((currentResult.Mean - baselineResult.Mean) / baselineResult.Mean) * 100;
                        return new ComparisonRow
                        {
                            Display = currentResult.Display,
                            Baseline = baselineResult.Mean,
                            Current = currentResult.Mean,
                            Change = change,
                            Failed = change > threshold
                        };
                    })
                    .ToArray();

                WriteMarkdown(
                    outputMarkdown,
                    rows,
                    threshold,
                    current.Keys.Except(baseline.Keys).OrderBy(key => key),
                    baseline.Keys.Except(current.Keys).OrderBy(key => key));

                foreach (var row in rows)
                {
                    var status = row.Failed ? "FAIL" : "PASS";
                    Console.WriteLine(
                        $"{status}: {row.Display} baseline={FormatNanoseconds(row.Baseline)} current={FormatNanoseconds(row.Current)} change={Percent(row.Change)}%");
                }

                if (rows.Any(row => row.Failed))
                {
                    Console.Error.WriteLine($"One or more benchmarks regressed by more than {Percent(threshold)}%.");
                    return 1;
                }

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
                        var mean = benchmark.GetProperty("Statistics").GetProperty("Mean").GetDouble();
                        var key = BenchmarkKey(benchmark);
                        results[key] = new BenchmarkResult
                        {
                            Mean = mean,
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

        private static string RequiredOption(string[] args, string name)
        {
            var value = Option(args, name, null);
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"Missing required option: {name}");
            }

            return value;
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
            string path,
            IEnumerable<ComparisonRow> rows,
            double thresholdPercent,
            IEnumerable<string> missingBaseline,
            IEnumerable<string> missingCurrent)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Benchmark Regression Check");
            builder.AppendLine();
            builder.AppendLine($"Threshold: current mean must not be more than {Percent(thresholdPercent)}% slower than baseline.");
            builder.AppendLine();
            builder.AppendLine("| Benchmark | Baseline mean | Current mean | Change | Result |");
            builder.AppendLine("| --- | ---: | ---: | ---: | --- |");

            foreach (var row in rows)
            {
                builder.AppendLine($"| {Markdown(row.Display)} | {FormatNanoseconds(row.Baseline)} | {FormatNanoseconds(row.Current)} | {Percent(row.Change)}% | {(row.Failed ? "fail" : "pass")} |");
            }

            AppendMissingBenchmarks(builder, "Benchmarks only present in current results:", missingBaseline);
            AppendMissingBenchmarks(builder, "Benchmarks only present in baseline results:", missingCurrent);
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
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
            if (value >= 1000000)
            {
                return $"{(value / 1000000).ToString("F3", CultureInfo.InvariantCulture)} ms";
            }

            if (value >= 1000)
            {
                return $"{(value / 1000).ToString("F3", CultureInfo.InvariantCulture)} us";
            }

            return $"{value.ToString("F3", CultureInfo.InvariantCulture)} ns";
        }

        private sealed class BenchmarkResult
        {
            public double Mean { get; set; }
            public string Display { get; set; }
        }

        private sealed class ComparisonRow
        {
            public string Display { get; set; }
            public double Baseline { get; set; }
            public double Current { get; set; }
            public double Change { get; set; }
            public bool Failed { get; set; }
        }
    }
}
