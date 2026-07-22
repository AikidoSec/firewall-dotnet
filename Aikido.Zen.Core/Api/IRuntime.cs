using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    internal interface IRuntimeAPIClient
    {
        Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token, CancellationToken cancellationToken);
        Task<ReportingAPIResponse> GetConfig(string token, CancellationToken cancellationToken);
        Task<HttpStatusCode> SubscribeToConfigUpdates(string token, Func<long, Task> onUpdate, CancellationToken cancellationToken);
    }
}
