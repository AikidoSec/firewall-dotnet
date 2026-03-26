using Aikido.Zen.Core.Api;
using System.Threading.Tasks;

namespace Aikido.Zen.Core.Api
{
    internal interface IReportingAPIClient
    {
        Task<ReportingAPIResponse> ReportAsync(string token, object @event);
        Task<FirewallListsAPIResponse> GetFirewallLists(string token);
    }
}
