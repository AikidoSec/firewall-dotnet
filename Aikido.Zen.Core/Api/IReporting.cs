using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    internal interface IReportingAPIClient
    {
        Task<ReportingAPIResponse> ReportAsync(string token, object @event, CancellationToken cancellationToken);
        Task<FirewallListsAPIResponse> GetFirewallLists(string token, CancellationToken cancellationToken);
    }
}
