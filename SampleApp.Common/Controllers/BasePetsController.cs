using System.Diagnostics;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SampleApp.Common.Models;

namespace SampleApp.Common.Controllers
{
    /// <summary>
    /// Base implementation of the pets controller that provides endpoint configuration
    /// </summary>
    public abstract class BasePetsController : IPetsController
    {
        /// <summary>
        /// Configures the endpoints for the pets API
        /// </summary>
        public virtual void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/pets", () =>
            {
                var pets = GetAllPets();
                return Results.Ok(pets);
            });

            endpoints.MapGet("/api/pets/{id:int}", (int id) =>
            {
                var pet = GetPetById(id);
                return pet != null ? Results.Ok(pet) : Results.NotFound();
            });

            endpoints.MapPost("/api/pets/create", async (HttpContext context) =>
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var petData = JsonSerializer.Deserialize<PetCreate>(body);

                if (petData == null || string.IsNullOrEmpty(petData.Name))
                {
                    return Results.BadRequest("Pet name is required, " + body);
                }

                var rowsCreated = CreatePetByName(petData.Name);
                return Results.Ok(new { Rows = rowsCreated });
            });

            endpoints.MapGet("/api/pets/command", (HttpContext context) =>
            {
                var command = context.Request.Query["command"];
                try
                {
                    var result = ExecuteCommand(command!);
                    return Results.Ok("command executed");
                }
                catch (Exception ex)
                {
                    var aikidoContext = context.Items["Aikido.Zen.Context"] as Aikido.Zen.Core.Context;
                    if (aikidoContext == null || aikidoContext.AttackDetected == false || !EnvironmentHelper.DryMode)
                        throw;
                    // this command doesn't work on windows, so let's pretend it worked
                    return Results.Ok("command executed");
                }
            });
        }

        public abstract IEnumerable<Pet> GetAllPets();
        public abstract Pet GetPetById(int id);
        public abstract int CreatePetByName(string name);

        /// <summary>
        /// Executes a command based on user input, simulating a command injection vulnerability.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <returns>The result of the command execution.</returns>
        public string ExecuteCommand(string command)
        {
            // Simulate command injection vulnerability
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}
