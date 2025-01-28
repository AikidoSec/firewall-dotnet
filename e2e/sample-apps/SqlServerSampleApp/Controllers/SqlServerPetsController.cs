using SampleApp.Common.Controllers;
using SampleApp.Common.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace SqlServerSampleApp.Controllers
{
    /// <summary>
    /// SQL Server implementation of the pets controller
    /// </summary>
    public class SqlServerPetsController : BasePetsController
    {
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
