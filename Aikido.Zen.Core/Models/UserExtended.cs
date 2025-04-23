using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Extended user information including tracking details.
    /// Inherits from HitCount to track usage for LFU eviction.
    /// </summary>
    public class UserExtended : HitCount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LastIpAddress { get; set; }
        public long LastSeenAt { get; set; }
        public long FirstSeenAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserExtended"/> class.
        /// </summary>
        /// <param name="id">The user ID.</param>
        /// <param name="name">The user name.</param>
        public UserExtended(string id, string name) : base()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                // throw an exception if the user ID or name is null or empty
                throw new System.ArgumentException("User ID or name cannot be null or empty");
            }
            Id = id;
            Name = name;
            LastIpAddress = string.Empty;
            FirstSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
            LastSeenAt = FirstSeenAt;
        }
    }
}
