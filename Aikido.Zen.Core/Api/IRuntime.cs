using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    public interface IRuntimeAPIClient
    {
        Task<ReportingAPIResponse> GetConfigVersion(string token);
        Task<ReportingAPIResponse> GetConfig(string token);
    }
}
