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
        private string _dangerousCharacterCommand;
        private string _dangerousCharacterUserInput;
        private string _dangerousCommand;
        private string _dangerousCommandUserInput;
        private string _longCommand;
        private string _safeCommand;
        private string _safeUserInput;

        [GlobalSetup]
        public void Setup()
        {
            _dangerousCharacterUserInput = "; rm -rf /; #";
            _dangerousCharacterCommand = "ls -la /home/user/" + _dangerousCharacterUserInput;
            _dangerousCommandUserInput = "rm";
            _dangerousCommand = "ls -la /home/user && rm -rf /";
            _safeUserInput = "documents";
            _safeCommand = "ls -la /home/user/documents";
            _longCommand = _dangerousCommand;

            for (int i = 0; i < 10; i++)
            {
                _longCommand = "echo benchmark" + i + " && " + _longCommand;
            }
        }

        [Benchmark]
        public bool DetectShellInjectionWithDangerousCharacters()
        {
            return ShellInjectionDetector.IsShellInjection(_dangerousCharacterCommand, _dangerousCharacterUserInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithDangerousCommand()
        {
            return ShellInjectionDetector.IsShellInjection(_dangerousCommand, _dangerousCommandUserInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithLongCommand()
        {
            return ShellInjectionDetector.IsShellInjection(_longCommand, _dangerousCommandUserInput);
        }

        [Benchmark]
        public bool DetectShellInjectionWithSafeInput()
        {
            return ShellInjectionDetector.IsShellInjection(_safeCommand, _safeUserInput);
        }
    }
}
