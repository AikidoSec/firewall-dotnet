namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents information about an API request body
    /// </summary>
    public class APIBodyInfo
    {
        /// <summary>
        /// Type of the body data
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Schema of the body data
        /// </summary>
        public DataSchema Schema { get; set; } = new DataSchema();
    }
}