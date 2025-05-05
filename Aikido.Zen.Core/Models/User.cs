using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class User
    {
        [JsonPropertyName("id")]
        public string Id { get; }
        [JsonPropertyName("name")]
        public string Name { get; }

        public User(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                // throw an exception if the user ID or name is null or empty
                throw new System.ArgumentException("User ID or name cannot be null or empty");
            }
            Id = id;
            Name = name;
        }

    }
}
