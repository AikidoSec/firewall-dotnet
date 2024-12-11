namespace Aikido.Zen.Core.Models {
    public class User
    {
        public string Id { get;}
        public string Name { get; }

        public User(string id, string name)
        {
            Id = id;
            Name = name;
        }

    }
}
