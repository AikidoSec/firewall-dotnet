using System.Text.Json.Serialization;

namespace SampleApp.Common.Models
{
    /// <summary>
    /// Represents a pet in the system
    /// </summary>
    public class Pet
    {
        public Pet(int id, string name)
        {
            Id = id;
            Name = name;
        }
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// DTO for creating a new pet
    /// </summary>
    public class PetCreate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
