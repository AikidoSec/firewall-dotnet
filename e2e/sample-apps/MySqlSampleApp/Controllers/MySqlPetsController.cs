using SampleApp.Common.Controllers;
using SampleApp.Common.Models;

namespace MySqlSampleApp.Controllers
{
    /// <summary>
    /// MySQL implementation of the pets controller
    /// </summary>
    public class MySqlPetsController : BasePetsController
    {
        private readonly DatabaseService _databaseService;

        public MySqlPetsController(DatabaseService databaseService)
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
