using SampleApp.Common.Models;

namespace SampleApp.Common.Controllers
{
    /// <summary>
    /// Interface defining the operations available for managing pets
    /// </summary>
    public interface IPetsController
    {
        /// <summary>
        /// Gets all pets from the database
        /// </summary>
        IEnumerable<Pet> GetAllPets();

        /// <summary>
        /// Gets a pet by its ID
        /// </summary>
        Pet GetPetById(int id);

        /// <summary>
        /// Creates a new pet with the given name
        /// </summary>
        int CreatePetByName(string name);
    }
}