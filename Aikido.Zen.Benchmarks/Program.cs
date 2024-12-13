using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;

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

            foreach (var summary in summaries)
            {
                // Export the results to a markdown file
                MarkdownExporter.Console.ExportToFiles(summary, BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            }
            
            Console.ReadLine();
        }
    }
}
