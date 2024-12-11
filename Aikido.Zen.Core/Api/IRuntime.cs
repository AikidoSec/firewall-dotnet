using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    public interface IRuntimeAPIClient
    {
        Task<CheckConfigAPIResponse> GetConfigVersion(string token);
        Task<CheckConfigAPIResponse> GetConfig(string token);
    }
}
