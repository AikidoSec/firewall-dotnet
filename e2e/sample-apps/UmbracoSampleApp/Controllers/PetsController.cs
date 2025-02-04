using SampleApp.Common.Controllers;
using SampleApp.Common.Models;

namespace UmbracoSampleApp.Controllers
{
    public class PetsController : BasePetsController
    {
        private readonly DatabaseService _databaseService;

        public PetsController(IServiceProvider serviceProvider)
        {
            _databaseService = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<DatabaseService>();
        }

        public override IEnumerable<Pet> GetAllPets()
        {
            return _databaseService.GetAllPets();
        }

        public override Pet GetPetById(int id)
        {
            return _databaseService.GetPetById(id);
        }

        public override int CreatePetByName(string name)
        {
            return _databaseService.CreatePetByName(name);
        }
    }
}
