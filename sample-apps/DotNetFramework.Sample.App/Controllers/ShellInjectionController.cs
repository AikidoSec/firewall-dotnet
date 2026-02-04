using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace DotNetFramework.Sample.App.Controllers
{
    /// <summary>
    /// Controller demonstrating potential shell injection vulnerabilities in .NET Framework.
    /// Executes commands provided via query parameters.
    /// </summary>
    [RoutePrefix("api/shell-injection")]
    public class ShellInjectionController : ApiController
    {
        private const int MaxDecodeUriPasses = 2;

        /// <summary>
        /// Executes a shell command provided in the 'cmd' query parameter.
        /// On Windows, it attempts to use WSL via 'cmd.exe /c wsl'.
        /// On Unix-like systems, it uses '/bin/bash -c'. Note: Running .NET Framework on non-Windows systems is less common but possible with Mono.
        /// </summary>
        /// <returns>An IHttpActionResult containing the command's stdout and stderr, or an error message.</returns>
        [HttpGet]
        [Route("execute")]
        public async Task<IHttpActionResult> ExecuteCommand()
        {
            var queryParams = Request.GetQueryNameValuePairs().ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            if (!queryParams.TryGetValue("cmd", out var command) || string.IsNullOrEmpty(command))
            {
                return BadRequest("Command parameter 'cmd' is required.");
            }

            command = DecodeUriComponent(command);

            var processStartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Removed platform check - always use cmd /c wsl
            processStartInfo.FileName = "cmd.exe";
            // Basic escaping - may need refinement for complex commands with WSL.
            processStartInfo.Arguments = $"/c wsl {command}";

            var output = new StringBuilder();
            var error = new StringBuilder();
            var processExited = new TaskCompletionSource<bool>();

            try
            {
                using (var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true })
                {
                    process.OutputDataReceived += (sender, args) => { if (args.Data != null) output.AppendLine(args.Data); };
                    process.ErrorDataReceived += (sender, args) => { if (args.Data != null) error.AppendLine(args.Data); };
                    process.Exited += (sender, args) => processExited.TrySetResult(true);

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for exit or timeout (10 seconds)
                    var completedTask = await Task.WhenAny(processExited.Task, Task.Delay(10000));

                    if (completedTask != processExited.Task) // Timeout
                    {
                        try
                        {
                            if (!process.HasExited) process.Kill();
                        }
                        catch { /* Ignore kill errors */ }
                        return Content(HttpStatusCode.InternalServerError, "Command execution timed out.");
                    }

                    // Process finished, ensure streams are flushed
                    process.WaitForExit();

                    string responseOutput = output.ToString();
                    string responseError = error.ToString();

                    if (process.ExitCode != 0)
                    {
                        return Content(HttpStatusCode.InternalServerError, $"Command failed with exit code {process.ExitCode}:{responseError}");
                    }

                    return Ok($"Output:{responseOutput}Error:{responseError}");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error executing command: {ex.Message} StackTrace:{ex.StackTrace}");
            }
        }

        private static string DecodeUriComponent(string input)
        {
            string decoded = input;

            if (string.IsNullOrEmpty(input))
            {
                return decoded;
            }

            for (int i = 0; i < MaxDecodeUriPasses; i++)
            {
                string next = Uri.UnescapeDataString(decoded);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
            }

            return decoded;
        }
    }
}
