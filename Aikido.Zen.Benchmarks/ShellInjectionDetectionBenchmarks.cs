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
    public class ShellInjectionDetectionBenchmarks
    {
        private string _command;
        private string _userInput;

        [GlobalSetup]
        public void Setup()
        {
            _command = "ls -la /home/user/";
            _userInput = "; rm -rf /; #";
        }

        [Benchmark]
        public bool DetectShellInjection()
        {
            return ShellInjectionDetector.IsShellInjection(_command, _userInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithLongCommand()
        {
            var longCommand = _command;
            for (int i = 0; i < 10; i++)
            {
                longCommand += " && " + _command;
            }
            return ShellInjectionDetector.IsShellInjection(longCommand, _userInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithLongUserInput()
        {
            var longUserInput = _userInput;
            for (int i = 0; i < 10; i++)
            {
                longUserInput += " && " + _userInput;
            }
            return ShellInjectionDetector.IsShellInjection(_command, longUserInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithSafeInput()
        {
            return ShellInjectionDetector.IsShellInjection(_command, "documents");
        }
    }
}
