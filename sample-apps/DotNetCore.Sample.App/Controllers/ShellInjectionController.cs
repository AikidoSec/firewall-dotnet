using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    /// <summary>
    /// Controller demonstrating potential shell injection vulnerabilities.
    /// Executes commands provided via query parameters.
    /// </summary>
    [ApiController]
    [Route("shell-injection")]
    public class ShellInjectionController : ControllerBase
    {
        /// <summary>
        /// Executes a shell command provided in the 'cmd' query parameter.
        /// On Windows, it attempts to use WSL via 'cmd.exe /c wsl'.
        /// On Unix-like systems, it uses '/bin/bash -c'.
        /// </summary>
        /// <returns>An IActionResult containing the command's stdout and stderr, or an error message.</returns>
        [HttpGet("execute")]
        public async Task<IActionResult> ExecuteCommand()
        {
            var command = Request.Query["cmd"].ToString();
            if (string.IsNullOrEmpty(command))
            {
                return BadRequest("Command parameter 'cmd' is required.");
            }

            return await ExecuteCommandInternal(command);
        }

        /// <summary>
        /// Executes a shell command provided as a route parameter.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <returns>An IActionResult containing the command's stdout and stderr, or an error message.</returns>
        [HttpGet("/api/execute/{command}")]
        public async Task<IActionResult> ExecuteRouteCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return BadRequest("Route parameter 'command' is required.");
            }

            return await ExecuteCommandInternal(command);
        }

        private async Task<IActionResult> ExecuteCommandInternal(string command)
        {
            var processStartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processStartInfo.FileName = "cmd.exe";
                // Need to escape quotes for cmd.exe if the command contains them.
                // This basic escaping handles simple cases.
                processStartInfo.Arguments = $"/c {command}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use /bin/bash -c on Unix-like systems
                processStartInfo.FileName = "/bin/bash";
                // Wrap the command in quotes for bash -c
                // Escape quotes within the command string for bash
                processStartInfo.Arguments = $"-c {command}";
            }
            else
            {
                return StatusCode(500, $"Unsupported operating system: {RuntimeInformation.OSDescription}");
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            var processExited = new TaskCompletionSource<bool>();

            try
            {
                using (var process = new Process { StartInfo = processStartInfo })
                {
                    // Ensure the process events are wired up correctly
                    process.EnableRaisingEvents = true; // Important for Exited event

                    process.OutputDataReceived += (sender, args) => { if (args.Data != null) output.AppendLine(args.Data); };
                    process.ErrorDataReceived += (sender, args) => { if (args.Data != null) error.AppendLine(args.Data); };
                    process.Exited += (sender, args) => processExited.TrySetResult(true);

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for the process to exit or timeout
                    var completedTask = await Task.WhenAny(processExited.Task, Task.Delay(10000)); // 10 seconds timeout

                    if (completedTask != processExited.Task) // If the delay task completed first, it's a timeout
                    {
                        try
                        {
                            if (!process.HasExited) process.Kill(true);
                        }
                        catch { /* Ignore errors trying to kill */ }
                        return StatusCode(500, "Command execution timed out.");
                    }

                    // Process exited within the timeout
                    // Ensure all output/error streams are flushed
                    // WaitForExit without timeout here ensures async handlers finish
                    process.WaitForExit();

                    // Use a simple format for the response string
                    string responseOutput = output.ToString();
                    string responseError = error.ToString();

                    if (process.ExitCode != 0)
                    {
                        return StatusCode(500, $"Command failed with exit code {process.ExitCode}:{responseError}");
                    }

                    return Ok($"Output:{responseOutput}Error:{responseError}");
                }
            }
            catch (Exception ex)
            {
                // Use a simple format for the response string
                return BadRequest($"Error executing command: {ex.Message}\nStackTrace:{ex.StackTrace}");
            }
        }
    }
}
