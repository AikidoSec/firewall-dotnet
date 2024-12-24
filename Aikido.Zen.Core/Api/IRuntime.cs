using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    public interface IRuntimeAPIClient
    {
        Task<ReportingAPIResponse> GetConfigLastUpdated(string token);
        Task<ReportingAPIResponse> GetConfig(string token);
    }
}
