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
    public class ShellInjectionDetectionBenchmarks
    {
        private const int DetectOperationsPerInvocation = 20_000_000;
        private const int LongCommandOperationsPerInvocation = 5_000_000;
        private const int LongUserInputOperationsPerInvocation = 50_000_000;
        private const int SafeInputOperationsPerInvocation = 20_000_000;

        private string _command;
        private string _userInput;
        private string _longCommand;
        private string _longUserInput;

        [GlobalSetup]
        public void Setup()
        {
            _command = "ls -la /home/user/";
            _userInput = "; rm -rf /; #";
            _longCommand = _command;
            _longUserInput = _userInput;

            for (int i = 0; i < 10; i++)
            {
                _longCommand += " && " + _command;
                _longUserInput += " && " + _userInput;
            }
        }

        [Benchmark]
        public int DetectShellInjection()
        {
            var detected = 0;
            for (int i = 0; i < DetectOperationsPerInvocation; i++)
            {
                detected += ShellInjectionDetector.IsShellInjection(_command, _userInput) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectShellInjectionWithLongCommand()
        {
            var detected = 0;
            for (int i = 0; i < LongCommandOperationsPerInvocation; i++)
            {
                detected += ShellInjectionDetector.IsShellInjection(_longCommand, _userInput) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectShellInjectionWithLongUserInput()
        {
            var detected = 0;
            for (int i = 0; i < LongUserInputOperationsPerInvocation; i++)
            {
                detected += ShellInjectionDetector.IsShellInjection(_command, _longUserInput) ? 1 : 0;
            }

            return detected;
        }

        [Benchmark]
        public int DetectShellInjectionWithSafeInput()
        {
            var detected = 0;
            for (int i = 0; i < SafeInputOperationsPerInvocation; i++)
            {
                detected += ShellInjectionDetector.IsShellInjection(_command, "documents") ? 1 : 0;
            }

            return detected;
        }
    }
}
