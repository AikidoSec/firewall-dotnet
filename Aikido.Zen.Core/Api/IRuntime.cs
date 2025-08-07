using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    internal interface IRuntimeAPIClient
    {
        Task<ReportingAPIResponse> GetConfigLastUpdated(string token);
        Task<ReportingAPIResponse> GetConfig(string token);
    }
}
