using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using SampleApp.Common.Models;
using Microsoft.AspNetCore.Routing;

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
        }

        public abstract IEnumerable<Pet> GetAllPets();
        public abstract Pet GetPetById(int id);
        public abstract int CreatePetByName(string name);
    }
}
