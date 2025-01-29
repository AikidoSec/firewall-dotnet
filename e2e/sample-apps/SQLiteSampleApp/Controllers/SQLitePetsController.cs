using SampleApp.Common.Controllers;
using SampleApp.Common.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace SQLiteSampleApp.Controllers
{
    /// <summary>
    /// SQLite implementation of the pets controller
    /// </summary>
    public class SQLitePetsController : BasePetsController
    {
        private readonly DatabaseService _databaseService;

        public SQLitePetsController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public override IEnumerable<Pet> GetAllPets()
        {
            return DatabaseService.GetAllPets();
        }

        public override Pet GetPetById(int id)
        {
            return DatabaseService.GetPetById(id);
        }

        public override int CreatePetByName(string name)
        {
            return DatabaseService.CreatePetByName(name);
        }

    }
}
