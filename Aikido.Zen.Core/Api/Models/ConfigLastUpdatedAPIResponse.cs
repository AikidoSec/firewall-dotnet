namespace Aikido.Zen.Core.Api
{
    /// <summary>
    /// Represents the response from the config last updated endpoint.
    /// </summary>
    public class ConfigLastUpdatedAPIResponse : APIResponse
    {
        /// <summary>
        /// Gets or sets the timestamp when the configuration was last updated.
        /// </summary>
        public long ConfigUpdatedAt { get; set; }
    }
}
