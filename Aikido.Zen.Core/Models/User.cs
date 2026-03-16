namespace Aikido.Zen.Core.Models {
    public class User
    {
        public string Id { get;}
        public string Name { get; }

        public User(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new System.ArgumentException("User ID cannot be null or empty");
            }
            Id = id;
            Name = name;
        }

    }
}
