using Aikido.Zen.Core.Sinks;
using System.Text.Json;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class ShellInjectionDetectorTests
    {
        [TestCaseSource(nameof(GetTestData))]
        public void IsShellInjection_ShouldDetectInjection(string command, string userInput, string description, bool expectedResult)
        {
            // Arrange

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult), description);
        }

        [Test]
        public void IsSafelyEncapsulated_Tests()
        {
            // Test: safe between single quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '`$USER'", "`USER"), Is.True);

            // Test: single quote in single quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo ''USER'", "'USER"), Is.False);

            // Test: dangerous chars between double quotes
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"=USER\"", "=USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\"", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"!USER\"", "!USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"`USER\"", "`USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"\\USER\"", "\\USER"), Is.False);

            // Test: same user input multiple times
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER' '$USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\" '$USER'", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER\" \"$USER\"", "$USER"), Is.False);

            // Test: the first and last quote doesn't match
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo '$USER\"", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"$USER'", "$USER"), Is.False);

            // Test: the first or last character is not an escape char
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo $USER'", "$USER"), Is.False);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo $USER\"", "$USER"), Is.False);

            // Test: user input does not occur in the command
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo 'USER'", "$USER"), Is.True);
            Assert.That(ShellInjectionDetector.IsSafelyEncapsulated("echo \"USER\"", "$USER"), Is.True);
        }

        [Test]
        public void IsShellInjection_ShouldDetectTildeInjection()
        {
            // Arrange
            string command = "echo ~user";
            string userInput = "~";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect tilde injection when userInput is '~' and command contains '~'.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectSeparatorBeforeAndAfter()
        {
            // Arrange
            string command = "echo whoami";
            string userInput = "whoami";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when separators are before and after the user input.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectNullByteEndedCommand()
        {
            // Arrange
            string command = "echo whoami\0";
            string userInput = "whoami\0";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when user input contains a null byte ending.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectSeparatorBeforeEndOfString()
        {
            // Arrange
            string command = "echo $USER";
            string userInput = "echo";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when separator is before and end of string after user input.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectStartOfStringSeparatorAfter()
        {
            // Arrange
            string command = "$USER echo";
            string userInput = "$USER";

            // Act
            var result = ShellInjectionDetector.IsShellInjection(command, userInput);

            // Assert
            Assert.That(result, Is.True, "Should detect injection when start of string and separator is after user input.");
        }

        [Test]
        public void IsShellInjection_ShouldDetectCarriageReturnAsSeparator()
        {
            // Carriage return in user input is flagged
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \rrm", "\rrm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \rrm -rf", "\rrm -rf"), Is.True);

            // Carriage return in user input when user input is the command
            Assert.That(ShellInjectionDetector.IsShellInjection("sleep\r10", "sleep\r10"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("shutdown\r-h\rnow", "shutdown\r-h\rnow"), Is.True);

            // Carriage return as separator between commands
            Assert.That(ShellInjectionDetector.IsShellInjection("ls\rrm", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("echo test\rrm -rf /", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("rm\rls", "rm"), Is.True);
        }

        [Test]
        public void IsShellInjection_ShouldDetectFormFeedAsSeparator()
        {
            // Form feed in user input is flagged
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \frm", "\frm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("ls \frm -rf", "\frm -rf"), Is.True);

            // Form feed in user input when user input is the command
            Assert.That(ShellInjectionDetector.IsShellInjection("sleep\f10", "sleep\f10"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("shutdown\f-h\fnow", "shutdown\f-h\fnow"), Is.True);

            // Form feed as separator between commands
            Assert.That(ShellInjectionDetector.IsShellInjection("ls\frm", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("echo test\frm -rf /", "rm"), Is.True);
            Assert.That(ShellInjectionDetector.IsShellInjection("rm\fls", "rm"), Is.True);
        }

        // sh and similar shells
        [TestCase("sh", "-c \"echo ok;id\"", "sh -c echo ok;id")]
        [TestCase("/bin/sh", "-c id", "/bin/sh -c id")]
        [TestCase("BASH.EXE", "-ec \"id\"", "BASH.EXE -ec id")]
        [TestCase("bash", "-ce \"id\"", "bash -ce id")]
        [TestCase("bash", "-lc \"echo ok;id\"", "bash -lc echo ok;id")]
        [TestCase("sh", "-e -c \"echo ok;id\"", "sh -e -c echo ok;id")]
        [TestCase("zsh", "-c \"echo ok;id\"", "zsh -c echo ok;id")]
        [TestCase("sh", "-c \"echo $1\" sh \"Tom & Jerry\"", "sh -c echo $1")]
        [TestCase("bash", "deploy.sh \"two words\"", null)]
        [TestCase("sh", "script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("sh", "-e script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("bash", "-o errexit script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("bash", "--rcfile config script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("sh", "-- script.sh -c \"Tom & Jerry\"", null)]
        [TestCase("sh", "-c", null)]
        // PowerShell
        [TestCase("powershell.exe", "-NoProfile -Command \"Get-Date; whoami\"", "powershell.exe -NoProfile -Command Get-Date; whoami")]
        [TestCase("pwsh", "-c Get-Date", "pwsh -c Get-Date")]
        [TestCase("pwsh", "-co \"Get-Date; whoami\"", "pwsh -co Get-Date; whoami")]
        [TestCase("powershell.exe", "-CoM \"Get-Date; whoami\"", "powershell.exe -CoM Get-Date; whoami")]
        [TestCase("pwsh", "-comm \"Get-Date; whoami\"", "pwsh -comm Get-Date; whoami")]
        [TestCase("pwsh", "-comma \"Get-Date; whoami\"", "pwsh -comma Get-Date; whoami")]
        [TestCase("pwsh", "-comman \"Get-Date; whoami\"", "pwsh -comman Get-Date; whoami")]
        [TestCase("pwsh", "/Command \"Get-Date; whoami\"", "pwsh /Command Get-Date; whoami")]
        [TestCase("pwsh", "--command \"Get-Date; whoami\"", "pwsh --command Get-Date; whoami")]
        [TestCase("powershell.exe", "\u2013Com \"Get-Date; whoami\"", "powershell.exe \u2013Com Get-Date; whoami")]
        [TestCase("pwsh", "\u2014Com \"Get-Date; whoami\"", "pwsh \u2014Com Get-Date; whoami")]
        [TestCase("pwsh", "\u2015Com \"Get-Date; whoami\"", "pwsh \u2015Com Get-Date; whoami")]
        [TestCase("pwsh", "-ExecutionPolicy Bypass -Command \"Get-Date; whoami\"", "pwsh -ExecutionPolicy Bypass -Command Get-Date; whoami")]
        [TestCase("pwsh", "-ep Bypass -Command \"Get-Date; whoami\"", "pwsh -ep Bypass -Command Get-Date; whoami")]
        [TestCase("pwsh", "-NoLogo -NoProfile -NonInteractive -Command \"Get-Date; whoami\"", "pwsh -NoLogo -NoProfile -NonInteractive -Command Get-Date; whoami")]
        [TestCase("pwsh", "-NoProfile -ExecutionPolicy Bypass -NonInteractive -Command \"Get-Date; whoami\"", "pwsh -NoProfile -ExecutionPolicy Bypass -NonInteractive -Command Get-Date; whoami")]
        [TestCase("pwsh", "-NoProfile -InputFormat Text -NonInteractive -Command \"Get-Date; whoami\"", "pwsh -NoProfile -InputFormat Text -NonInteractive -Command Get-Date; whoami")]
        [TestCase("pwsh", "-inp xMl -Command \"Get-Date; whoami\"", "pwsh -inp xMl -Command Get-Date; whoami")]
        [TestCase("pwsh", "-WorkingDirectory \"Tom & Jerry\" -Command \"Write-Output ok\"", "pwsh -Command Write-Output ok")]
        [TestCase("pwsh", "-NoProfile -wd \"Tom & Jerry\" -NonInteractive -Command \"Get-Date; whoami\"", "pwsh -Command Get-Date; whoami")]
        [TestCase("pwsh", "-WorkingDirectory -Command -Command \"Get-Date; whoami\"", "pwsh -Command Get-Date; whoami")]
        [TestCase("powershell.exe", "-Sta -NoExit -Command \"Get-Date\"", "powershell.exe -Sta -NoExit -Command Get-Date")]
        [TestCase("powershell.exe", "-Mta -Command \"Get-Date\"", "powershell.exe -Mta -Command Get-Date")]
        [TestCase("pwsh", "-File script.ps1 \"Tom & Jerry\"", null)]
        [TestCase("pwsh", "-File script.ps1 -Com \"Tom & Jerry\"", null)]
        [TestCase("pwsh", "-f script.ps1 -Command \"Tom & Jerry\"", null)]
        [TestCase("pwsh", "-ConfigurationName config", null)]
        [TestCase("pwsh", "-CommandWithArgs \"Write-Output $args[0]\" -Com \"Tom & Jerry\"", null)]
        [TestCase("pwsh", "-cwa \"Write-Output $args[0]\" -Com \"Tom & Jerry\"", null)]
        [TestCase("pwsh", "\"\" - / --", null)]
        [TestCase("pwsh", "---command \"Get-Date; whoami\"", null)]
        [TestCase("pwsh", "//Com \"Get-Date; whoami\"", null)]
        [TestCase("pwsh", "-\u2013Com \"Get-Date; whoami\"", null)]
        // cmd.exe and batch files
        [TestCase("cmd.exe", "/c \"echo ok & whoami\"", "cmd.exe /c echo ok & whoami")]
        [TestCase("cmd.exe", "/d /s /c \"echo ok & whoami\"", "cmd.exe /d /s /c echo ok & whoami")]
        [TestCase("cmd.exe", "/q /c \"echo ok & whoami\"", "cmd.exe /q /c echo ok & whoami")]
        [TestCase("cmd.exe", "/v:on /c \"echo ok & whoami\"", "cmd.exe /v:on /c echo ok & whoami")]
        [TestCase("cmd.exe", "/d /q /v:on /c \"echo ok & whoami\"", "cmd.exe /d /q /v:on /c echo ok & whoami")]
        [TestCase("cmd.exe", "/q /k \"echo ok & whoami\"", "cmd.exe /q /k echo ok & whoami")]
        [TestCase("cmd.exe", "/k \"echo ok & whoami\"", "cmd.exe /k echo ok & whoami")]
        [TestCase("cmd.exe", "/d /s /k \"echo ok & whoami\"", "cmd.exe /d /s /k echo ok & whoami")]
        [TestCase("cmd.exe", "/s /d /K \"echo ok & whoami\"", "cmd.exe /s /d /K echo ok & whoami")]
        [TestCase("cmd.exe", " \t/d /s /k \"echo ok & whoami\"", "cmd.exe  \t/d /s /k echo ok & whoami")]
        [TestCase("cmd.exe", " \t/C \"echo ok & whoami\"", "cmd.exe  \t/C echo ok & whoami")]
        [TestCase("cmd.exe", "/c \"echo FIRST\necho SECOND\"", "cmd.exe /c echo FIRST\necho SECOND")]
        [TestCase("cmd.exe", "/c \"echo \"inner\" tail\" suffix", "cmd.exe /c echo \"inner\" tail suffix")]
        [TestCase("cmd.exe", "/c \"echo ok", "cmd.exe /c \"echo ok")]
        [TestCase("cmd.exe", "/c \"echo ${prefix} ${command} $1\"", "cmd.exe /c echo ${prefix} ${command} $1")]
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
        public void GetShellCommand_ReturnsNormalizedCommand(string fileName, string arguments, string? expected)
        {
            Assert.That(ShellInjectionDetector.GetShellCommand(fileName, arguments, ProcessExecutionSink.DecodeProcessArguments(arguments).ToList()), Is.EqualTo(expected));
        }

        [TestCase("sh", new[] { "-c", "echo \"$1\"", "sh", "Tom & Jerry" }, "sh -c echo \"$1\"")]
        [TestCase("bash", new[] { "-lc", "echo ok;id" }, "bash -lc echo ok;id")]
        [TestCase("sh", new[] { "-e", "-c", "echo ok;id" }, "sh -e -c echo ok;id")]
        [TestCase("pwsh", new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-Date; whoami" }, "pwsh -NoLogo -NoProfile -NonInteractive -Command Get-Date; whoami")]
        [TestCase("cmd.exe", new[] { "/c", "echo", "Tom & Jerry" }, "cmd.exe /c echo \"Tom & Jerry\"")]
        [TestCase("cmd.exe", new[] { "/c", "echo ok & whoami" }, "cmd.exe /c echo ok & whoami")]
        [TestCase("cmd.exe", new[] { "/q", "/c", "echo ok & whoami" }, "cmd.exe /q /c echo ok & whoami")]
        [TestCase("cmd.exe", new[] { "/v:on", "/c", "echo ok & whoami" }, "cmd.exe /v:on /c echo ok & whoami")]
        [TestCase("cmd.exe", new[] { "/k", "echo ok & whoami" }, "cmd.exe /k echo ok & whoami")]
        [TestCase("cmd.exe", new[] { "/d", "/s", "/k", "echo ok & whoami" }, "cmd.exe /d /s /k echo ok & whoami")]
        [TestCase("deploy.bat", new[] { "\"&whoami" }, "deploy.bat \"\"&whoami\"")]
        [TestCase("bash", new[] { "--rcfile", "Tom & Jerry", "-c", "printf ok" }, null)]
        [TestCase("pwsh", new[] { "-WorkingDirectory", "Tom & Jerry", "-Command", "Write-Output ok" }, "pwsh -Command Write-Output ok")]
        [TestCase("pwsh", new[] { "-if", "XML", "-Command", "Get-Date; whoami" }, "pwsh -if XML -Command Get-Date; whoami")]
        [TestCase("pwsh", new[] { "-wo", "Tom & Jerry", "-Command", "Get-Date; whoami" }, "pwsh -Command Get-Date; whoami")]
        [TestCase("sh", new[] { "-e -c", "echo ok;id" }, null)]
        [TestCase("sh", new[] { "-cfoo", "echo ok;id" }, null)]
        [TestCase("sh", new[] { "-c\n", "echo ok;id" }, null)]
        [TestCase("pwsh", new[] { "-NoProfile -Command", "Get-Date; whoami" }, null)]
        [TestCase("pwsh", new[] { "-ExecutionPolicy Bypass", "-Command", "Get-Date; whoami" }, null)]
        [TestCase("pwsh", new[] { "-ExecutionPolicy", "Bypass", "-ExecutionPolicy", "Bypass", "-Command", "Get-Date; whoami" }, null)]
        [TestCase("pwsh", new[] { "-Unknown", "-Command", "Get-Date; whoami" }, null)]
        public void GetShellCommand_WithArgumentList_ReturnsNormalizedCommand(string fileName, string[] arguments, string? expected)
        {
            Assert.That(ShellInjectionDetector.GetShellCommand(fileName, string.Empty, arguments.ToList()), Is.EqualTo(expected));
        }

        [TestCase("bash", "-o errexit -c \"echo FIRST; echo SECOND\"")]
        [TestCase("bash", "-eo errexit -c \"printf ok\"")]
        [TestCase("bash", "+o errexit -c \"printf ok\"")]
        [TestCase("bash", "-O extglob -c \"printf ok\"")]
        [TestCase("bash", "--rcfile \"Tom & Jerry\" -c \"printf ok\"")]
        [TestCase("bash", "--init-file \"Tom & Jerry\" -c \"printf ok\"")]
        [TestCase("fish", "-d exec -c \"echo FIRST; echo SECOND\"")]
        [TestCase("sh", "-nc \"echo FIRST; echo SECOND\"")]
        [TestCase("fish", "-C \"echo ok;id\" -c \"printf safe\"")]
        [TestCase("pwsh", "-InputFormat")]
        [TestCase("pwsh", "-InputFormat -Command \"Get-Date; whoami\"")]
        [TestCase("pwsh", "-InputFormat JSON -Command \"Get-Date; whoami\"")]
        [TestCase("pwsh", "-InputFormat \" Text \" -Command \"Get-Date; whoami\"")]
        [TestCase("pwsh", "-WorkingDirectory")]
        [TestCase("pwsh", "-WorkingDirectory -Command \"Get-Date; whoami\"")]
        [TestCase("pwsh", "-WorkingDirectory directory -InputFormat Text -Command \"Get-Date; whoami\"")]
        [TestCase("sh", "-cfoo \"echo ok;id\"")]
        [TestCase("sh", "-c\n \"echo ok;id\"")]
        [TestCase("sh", "\"-e -c\" \"echo ok;id\"")]
        [TestCase("sh", "-unknown -c \"echo ok;id\"")]
        [TestCase("pwsh", "-Unknown -Command \"Get-Date; whoami\"")]
        [TestCase("pwsh", "\"-NoProfile -Command\" \"Get-Date; whoami\"")]
        [TestCase("pwsh", "-ExecutionPolicy Bypass -ExecutionPolicy Bypass -Command \"Get-Date; whoami\"")]
        public void GetShellCommand_WithUnsupportedOptions_ReturnsNull(string fileName, string arguments)
        {
            Assert.That(ShellInjectionDetector.GetShellCommand(fileName, arguments, ProcessExecutionSink.DecodeProcessArguments(arguments).ToList()), Is.Null);
        }

        [TestCase("Bypass")]
        [TestCase("bYpAsS")]
        public void GetShellCommand_WithExecutionPolicy_ReturnsNormalizedCommand(string policy)
        {
            var arguments = new[] { "-ExecutionPolicy", policy, "-Command", "Get-Date; whoami" }.ToList();

            Assert.That(ShellInjectionDetector.GetShellCommand("pwsh", string.Empty, arguments), Is.EqualTo($"pwsh -ExecutionPolicy {policy} -Command Get-Date; whoami"));
        }

        [TestCase("")]
        [TestCase("RemoteSigned")]
        [TestCase("AllSigned")]
        [TestCase("Restricted")]
        [TestCase("Unrestricted")]
        [TestCase("Default")]
        [TestCase("Undefined")]
        [TestCase(" Bypass ")]
        [TestCase("Bypass extra")]
        [TestCase("Bypass\n")]
        [TestCase("Bypass;id")]
        [TestCase("Tom & Jerry")]
        [TestCase("Unexpected")]
        public void GetShellCommand_WithUnsupportedExecutionPolicy_ReturnsNull(string policy)
        {
            var arguments = new[] { "-ExecutionPolicy", policy, "-Command", "Get-Date; whoami" }.ToList();

            Assert.That(ShellInjectionDetector.GetShellCommand("pwsh", string.Empty, arguments), Is.Null);
        }

        [TestCase("/c powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\"")]
        [TestCase("/c \"powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\"\"")]
        public void GetShellCommand_WithNestedPowerShell_OnlyNormalizesCmd(string arguments)
        {
            Assert.That(ShellInjectionDetector.GetShellCommand("cmd.exe", arguments, ProcessExecutionSink.DecodeProcessArguments(arguments).ToList()),
                Is.EqualTo("cmd.exe /c powershell.exe -Command \"Write-Output FIRST; Write-Output SECOND\""));
        }

        [TestCase("sh", "-c \"echo a\";id;\"\"", "a\";id;\"", true)]
        [TestCase("sh", "-c \"echo \\\"ok\\\";id\"", "echo \"ok\";id", true)]
        [TestCase("sh", "-c \"id\"", "id", true)]
        [TestCase("bash", "-o errexit -c whoami", "whoami", true)]
        [TestCase("bash", "-c \"grep 'foo;id' /var/log/app.log\"", "foo;id", false)]
        [TestCase("git", "commit -m \"Tom & Jerry\" -- sh", "Tom & Jerry", false)]
        [TestCase("cmd.exe", "/q /c \"echo \"Tom & Jerry\"\"", "Tom & Jerry", false)]
        [TestCase("cmd.exe", "/q /c echo \"a\" /c\"Tom & Jerry\"", "Tom & Jerry", false)]
        [TestCase("sh", "-e script.sh -c \"Tom & Jerry\"", "Tom & Jerry", false)]
        [TestCase("bash", "--rcfile \"Tom & Jerry\" -c \"printf ok\"", "Tom & Jerry", false)]
        [TestCase("pwsh", "-WorkingDirectory \"Tom & Jerry\" -Command \"Write-Output ok\"", "Tom & Jerry", false)]
        [TestCase("pwsh", "-WorkingDirectory \"Tom & Jerry\" -Command \"Write-Output 'Tom & Jerry'\"", "Tom & Jerry", false)]
        [TestCase("pwsh", "-WorkingDirectory \"Tom & Jerry\" -Command \"Write-Output 'Tom & Jerry'; Tom & Jerry\"", "Tom & Jerry", true)]
        [TestCase("pwsh", "-NoProfile -InputFormat Text -NonInteractive -Command \"Get-Date; whoami\"", "Get-Date; whoami", true)]
        [TestCase("pwsh", "-NoProfile -wd \"Tom & Jerry\" -NonInteractive -Command \"Get-Date; whoami\"", "Get-Date; whoami", true)]
        public void GetCommandsToInspect_WithArguments_DetectsInjection(string fileName, string rawArguments, string userInput, bool expected)
        {
            var arguments = ProcessExecutionSink.DecodeProcessArguments(rawArguments).ToList();
            var commands = ShellInjectionDetector.GetCommandsToInspect(fileName, rawArguments, arguments);

            Assert.That(commands.Any(command => command != null && ShellInjectionDetector.IsShellInjection(command, userInput)), Is.EqualTo(expected));
        }

        [TestCase("sh", new[] { "-c", "echo \"ok\";id" }, "echo \"ok\";id", true)]
        [TestCase("pwsh", new[] { " -Com ", "Get-Date; whoami" }, "Get-Date; whoami", true)]
        [TestCase("pwsh", new[] { "-NoProfile", "-Command", "whoami" }, "-NoProfile -Command whoami", true)]
        [TestCase("bash", new[] { "-e", "-c", "id" }, "-e -c id", true)]
        [TestCase("sh", new[] { "-c", "id" }, "sh -c id", true)]
        [TestCase("sh", new[] { "-c", "whoami\0" }, "whoami\0", true)]
        [TestCase("deploy.bat", new[] { "\"&whoami" }, "\"&whoami", true)]
        [TestCase("deploy.bat", new[] { "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("git", new[] { "commit", "-m", "Tom & Jerry; $(id)" }, "Tom & Jerry; $(id)", false)]
        [TestCase("cmd.exe", new[] { "/v:on", "/c", "echo", "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("sh", new[] { "-c", "printf '%s' \"Tom & Jerry\"" }, "Tom & Jerry", false)]
        [TestCase("sh", new[] { "-c", "echo \"$1\"", "sh", "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("sh", new[] { "script.sh", "-c", "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("pwsh", new[] { "-File", "script.ps1", "-Com", "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("pwsh", new[] { "-CommandWithArgs", "Write-Output $args[0]", "-Com", "Tom & Jerry" }, "Tom & Jerry", false)]
        [TestCase("sh", new[] { "-c", "" }, "", false)]
        [TestCase("pwsh", new[] { "-Command", "" }, "", false)]
        [TestCase("pwsh", new[] { "-NoProfile", "-if", "text", "-NonInteractive", "-Command", "Get-Date; whoami" }, "Get-Date; whoami", true)]
        [TestCase("pwsh", new[] { "-NoProfile", "-wo", "Tom & Jerry", "-NonInteractive", "-Command", "Get-Date; whoami" }, "Get-Date; whoami", true)]
        [TestCase("pwsh", new[] { "-WorkingDirectory", "Tom & Jerry", "-Command", "Write-Output ok" }, "Tom & Jerry", false)]
        [TestCase("pwsh", new[] { "-WorkingDirectory", "Tom & Jerry", "-Command", "Write-Output 'Tom & Jerry'" }, "Tom & Jerry", false)]
        [TestCase("pwsh", new[] { "-WorkingDirectory", "Tom & Jerry", "-Command", "Write-Output 'Tom & Jerry'; Tom & Jerry" }, "Tom & Jerry", true)]
        public void GetCommandsToInspect_WithArgumentList_DetectsInjection(string fileName, string[] arguments, string userInput, bool expected)
        {
            var commands = ShellInjectionDetector.GetCommandsToInspect(fileName, string.Empty, arguments.ToList());

            Assert.That(commands.Any(command => command != null && ShellInjectionDetector.IsShellInjection(command, userInput)), Is.EqualTo(expected));
        }

        public static IEnumerable<TestCaseData> GetTestData()
        {
            var jsonData = File.ReadAllText("testdata/data.ShellInjectionDetector.json");
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            foreach (var testCase in testCases)
            {
                yield return new TestCaseData(
                    testCase.Command,
                    testCase.UserInput,
                    testCase.Description,
                    testCase.IsInjection
                ).SetName($"Test_{testCase.Description}");
            }
        }

        private class TestCase
        {
            public string? Command { get; set; }
            public string? UserInput { get; set; }
            public string? Description { get; set; }
            public bool IsInjection { get; set; }
        }
    }
}
