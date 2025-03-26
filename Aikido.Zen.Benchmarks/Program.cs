using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.IO;

namespace Aikido.Zen.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var summaries = new List<Summary>();
            Console.WriteLine("Running patch benchmarks...");
            summaries.Add(BenchmarkRunner.Run<PatchBenchmarks>());

            Console.WriteLine("Running http helper benchmarks...");
            summaries.Add(BenchmarkRunner.Run<HttpHelperBenchmarks>());

            Console.WriteLine("Running sql detection benchmarks...");
            summaries.Add(BenchmarkRunner.Run<SQLInjectionDetectionBenchmarks>());

            Console.WriteLine("Running shell detection benchmarks...");
            summaries.Add(BenchmarkRunner.Run<ShellInjectionDetectionBenchmarks>());

            Console.WriteLine("Running rate limiting helper benchmarks...");
            summaries.Add(BenchmarkRunner.Run<RateLimitingHelperBenchmarks>());

            Console.WriteLine("Running lru cache benchmarks...");
            summaries.Add(BenchmarkRunner.Run<LRUCacheBenchmarks>());

            Console.WriteLine("Running block list benchmarks...");
            summaries.Add(BenchmarkRunner.Run<BlockListBenchmarks>());

            Console.WriteLine("Running agent context benchmarks...");
            summaries.Add(BenchmarkRunner.Run<AgentContextBenchmarks>());

            foreach (var summary in summaries)
            {
                Console.WriteLine("Saving summary at " + summary.ResultsDirectoryPath);
                Directory.CreateDirectory(summary.ResultsDirectoryPath);
                // Export the results to a markdown file
                MarkdownExporter.Console.ExportToFiles(summary, BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            }

            Console.ReadLine();
        }
    }
}
