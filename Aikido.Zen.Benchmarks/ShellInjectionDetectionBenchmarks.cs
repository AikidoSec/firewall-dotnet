using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using Aikido.Zen.Core.Vulnerabilities;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class ShellInjectionDetectionBenchmarks
    {
        private string _command;
        private string _userInput;
        private string _longCommand;
        private string _longUserInput;
        private string _longUserInputCommand;
        private string _safeCommand;

        [GlobalSetup]
        public void Setup()
        {
            _userInput = "; rm -rf /; #";
            _command = "ls -la /home/user/" + _userInput;
            _safeCommand = "ls -la /home/user/documents";
            _longCommand = _command;
            _longUserInput = _userInput;

            for (int i = 0; i < 10; i++)
            {
                _longCommand += " && " + _command;
                _longUserInput += " && " + _userInput;
            }

            _longUserInputCommand = "ls -la /home/user/" + _longUserInput;
        }

        [Benchmark]
        public bool DetectShellInjection()
        {
            return ShellInjectionDetector.IsShellInjection(_command, _userInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithLongCommand()
        {
            return ShellInjectionDetector.IsShellInjection(_longCommand, _userInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithLongUserInput()
        {
            return ShellInjectionDetector.IsShellInjection(_longUserInputCommand, _longUserInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithSafeInput()
        {
            return ShellInjectionDetector.IsShellInjection(_safeCommand, "documents");
        }
    }
}
