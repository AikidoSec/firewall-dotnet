using System;

namespace Aikido.Zen.Core.Models
{
    public class UserExtended : User, ICloneable
    {
        public string LastIpAddress { get; set; }
        public long FirstSeenAt { get; set; }
        public long LastSeenAt { get; set; }

        public object Clone()
        {
            return new UserExtended
            {
                Id = Id,
                Name = Name,
                LastIpAddress = LastIpAddress,
                FirstSeenAt = FirstSeenAt,
                LastSeenAt = LastSeenAt
            };
        }
    }
}
