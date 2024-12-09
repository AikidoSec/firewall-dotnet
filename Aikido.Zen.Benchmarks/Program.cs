using BenchmarkDotNet.Running;
using System;

namespace Aikido.Zen.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Console.WriteLine("Running patch benchmarks...");
            // BenchmarkRunner.Run<PatchBenchmarks>();

            Console.WriteLine("Running http helper benchmarks...");
            BenchmarkRunner.Run<HttpHelperBenchmarks>();
            
            Console.ReadLine();
        }
    }
}
