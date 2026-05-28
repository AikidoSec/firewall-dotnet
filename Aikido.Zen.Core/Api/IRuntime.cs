using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    internal interface IRuntimeAPIClient
    {
        Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token, CancellationToken cancellationToken);
        Task<ReportingAPIResponse> GetConfig(string token, CancellationToken cancellationToken);
    }
}
