using SampleApp.Common.Controllers;
using SampleApp.Common.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace PostgresSampleApp.Controllers
{
    /// <summary>
    /// PostgreSQL implementation of the pets controller
    /// </summary>
    public class PostgresPetsController : BasePetsController
    {
        private readonly DatabaseService _databaseService;

        public PostgresPetsController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public override IEnumerable<Pet> GetAllPets()
        {
            return DatabaseService.GetAllPets().Select(p => new Pet(p.Id, p.Name));
        }

        public override Pet GetPetById(int id)
        {
            var pet = DatabaseService.GetPetById(id);
            return new Pet(pet.Id, pet.Name);
        }

        public override int CreatePetByName(string name)
        {
            return DatabaseService.CreatePetByName(name);
        }
    }
}
