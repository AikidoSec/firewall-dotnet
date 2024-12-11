using System;

namespace Aikido.Zen.Core.Models
{
    public class UserExtended : User
    {
        public string LastIpAddress { get; set; }
        public long FirstSeenAt { get; set; }
        public long LastSeenAt { get; set; }

        public UserExtended(string id, string name) : base(id, name)
        {
            
        }
    }
}
