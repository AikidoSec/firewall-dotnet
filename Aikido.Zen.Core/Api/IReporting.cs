using Aikido.Zen.Core.Api;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    public interface IReportingAPIClient
    {
        Task<ReportingAPIResponse> ReportAsync(string token, object @event, int timeoutInMS);
        Task<FirewallListsAPIResponse> GetFirewallLists(string token);
    }
}
