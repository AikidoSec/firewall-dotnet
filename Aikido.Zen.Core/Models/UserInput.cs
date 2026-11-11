using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public readonly struct UserInput
    {
        [JsonConstructor]
        public UserInput(string location, string value)
        {
            Location = location;
            Value = value;
        }

        public string Location { get; }
        public string Value { get; }
    }
}
