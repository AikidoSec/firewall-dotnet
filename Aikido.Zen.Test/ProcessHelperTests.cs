using System.Diagnostics;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ProcessHelperTests
    {
        [TestCase("", new string[0])]
        [TestCase("   ", new string[0])]
        [TestCase("-c \"echo ok;id\"", new[] { "-c", "echo ok;id" })]
        [TestCase("  a \t \"b c\"  d  ", new[] { "a", "b c", "d" })]
        [TestCase("a\"b c\"d", new[] { "ab cd" })]
        [TestCase("\"\"", new[] { "" })]
        [TestCase("\"\" \"\"", new[] { "", "" })]
        [TestCase("a\"\"b", new[] { "ab" })]
        [TestCase("\"\"\"", new[] { "\"" })]
        [TestCase("\"\"\"\"", new[] { "\"" })]
        [TestCase("\"ab\"cd", new[] { "abcd" })]
        [TestCase("a\nb a\rb", new[] { "a\nb", "a\rb" })]
        [TestCase("\"a b", new[] { "a b" })]
        [TestCase("\"a\\\"b\"", new[] { "a\"b" })]
        [TestCase("\"a\"\"b\"", new[] { "a\"b" })]
        [TestCase("a\\\\b a\\", new[] { "a\\\\b", "a\\" })]
        [TestCase("\"a\\\\\" b", new[] { "a\\", "b" })]
        [TestCase("a\\\\\\\"b", new[] { "a\\\"b" })]
        [TestCase("-c \"echo 'x y'\"", new[] { "-c", "echo 'x y'" })]
        [TestCase("'a b'", new[] { "'a", "b'" })]
        [TestCase("-c \"echo a\";id;\"\"", new[] { "-c", "echo a;id;" })]
        public void ParseArguments_SplitsLikeProcessStart(string arguments, string[] expected)
        {
            Assert.That(ProcessHelper.ParseArguments(arguments), Is.EqualTo(expected));
        }

        [Test]
        public void GetArgv_ReturnsFileNameAndArguments()
        {
            Assert.That(ProcessHelper.GetArgv(new ProcessStartInfo("sh", "-c \"echo ok;id\"")), Is.EqualTo(new[] { "sh", "-c", "echo ok;id" }));
            Assert.That(ProcessHelper.GetArgv(new ProcessStartInfo("sh", new[] { "-c", "echo ok;id" })), Is.EqualTo(new[] { "sh", "-c", "echo ok;id" }));
            Assert.That(ProcessHelper.GetArgv(new ProcessStartInfo("sh")), Is.EqualTo(new[] { "sh" }));
        }

        // sh and similar shells
        [TestCase("sh", "-c \"echo ok;id\"", "sh -c echo ok;id")]
        [TestCase("/bin/sh", "-c id", "/bin/sh -c id")]
        [TestCase("BASH.EXE", "-ec \"id\"", "BASH.EXE -ec id")]
        [TestCase("sh", "-c \"echo $1\" sh \"Tom & Jerry\"", "sh -c echo $1")]
        [TestCase("bash", "-o errexit -c \"echo FIRST; echo SECOND\"", "bash -o errexit -c echo FIRST; echo SECOND")]
        [TestCase("bash", "-eo errexit -c \"echo FIRST; echo SECOND\"", "bash -eo errexit -c echo FIRST; echo SECOND")]
        [TestCase("fish", "-d exec -c \"echo FIRST; echo SECOND\"", "fish -d exec -c echo FIRST; echo SECOND")]
        [TestCase("bash", "deploy.sh \"two words\"", null)]
        [TestCase("sh", "-- script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("sh", "-c", null)]
        // PowerShell
        [TestCase("powershell.exe", "-NoProfile -Command \"Get-Date; whoami\"", "powershell.exe -NoProfile -Command Get-Date; whoami")]
        [TestCase("pwsh", "-c Get-Date", "pwsh -c Get-Date")]
        [TestCase("pwsh", "-ExecutionPolicy Bypass -Command \"Get-Date; whoami\"", "pwsh -ExecutionPolicy Bypass -Command Get-Date; whoami")]
        [TestCase("pwsh", "-ep Bypass -Command \"Get-Date; whoami\"", "pwsh -ep Bypass -Command Get-Date; whoami")]
        [TestCase("pwsh", "-File script.ps1 \"Tom & Jerry\"", null)]
        // cmd.exe and batch files
        [TestCase("cmd.exe", "/c \"echo ok & whoami\"", "cmd.exe /c echo ok & whoami")]
        [TestCase("cmd.exe", "/d /s /c \"echo ok & whoami\"", "cmd.exe /d /s /c echo ok & whoami")]
        [TestCase("cmd.exe", "/c echo \"Tom & Jerry\"", "cmd.exe /c echo \"Tom & Jerry\"")]
        [TestCase("cmd.exe", "/c echo \"a\" /c\"Tom & Jerry\"", "cmd.exe /c echo \"a\" /c\"Tom & Jerry\"")]
        [TestCase("C:\\Windows\\System32\\CMD.EXE", "/C \"echo \"a b\"\" x", "C:\\Windows\\System32\\CMD.EXE /C echo \"a b\" x")]
        [TestCase("C:\\scripts\\deploy.BAT", "\"Tom & Jerry\"", "C:\\scripts\\deploy.BAT \"Tom & Jerry\"")]
        // No shell
        [TestCase("git", "log --author sh", null)]
        [TestCase("git", "commit -m \"add fish recipe\"", null)]
        [TestCase("git", "commit -m \"a|b<c>\\\"d\"", null)]
        [TestCase("ssh", "-c aes256-ctr host", null)]
        [TestCase("sudo", "-u www sh -c \"id\" sh data", null)]
        [TestCase("docker", "exec app sh -c \"echo ok;id\"", null)]
        [TestCase("ssh", "host \"echo FIRST; echo SECOND\" sh -c \"echo safe\"", null)]
        [TestCase("echo", "sh -c \"Tom & Jerry\"", null)]
        [TestCase("/usr/bin/shasum", "-c sums.txt", null)]
        [TestCase("bash.sh", "-c id", null)]
        [TestCase("", "", null)]
        public void GetCommandsToInspect_ReturnsOriginalAndNormalizedCommands(string fileName, string arguments, string? expected)
        {
            var original = fileName + " " + arguments;
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo(fileName, arguments)),
                Is.EqualTo(expected == null ? new[] { original } : new[] { original, expected }));
        }

        [Test]
        public void GetCommandsToInspect_WithArgumentList_ReturnsOriginalAndNormalizedCommands()
        {
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo("sh", new[] { "-c", "echo \"$1\"", "sh", "Tom & Jerry" })), Is.EqualTo(new[] { "sh ", "sh -c echo \"$1\"" }));
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo("cmd.exe", new[] { "/c", "echo", "Tom & Jerry" })), Is.EqualTo(new[] { "cmd.exe ", "cmd.exe /c echo \"Tom & Jerry\"" }));
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo("cmd.exe", new[] { "/c", "echo ok & whoami" })), Is.EqualTo(new[] { "cmd.exe ", "cmd.exe /c echo ok & whoami" }));
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo("deploy.bat", new[] { "\"&whoami" })), Is.EqualTo(new[] { "deploy.bat ", "deploy.bat \"\"&whoami\"" }));
        }

        [TestCase("/c powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\"")]
        [TestCase("/c \"powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\"\"")]
        public void GetCommandsToInspect_WithNestedPowerShell_OnlyNormalizesCmd(string arguments)
        {
            Assert.That(ProcessHelper.GetCommandsToInspect(new ProcessStartInfo("cmd.exe", arguments)), Is.EqualTo(new[]
            {
                "cmd.exe " + arguments,
                "cmd.exe /c powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\""
            }));
        }

    }
}
