using Aikido.Zen.Core.Api;
using System.Threading.Tasks;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core.Api { 
	public interface IReportingAPIClient
	{
		Task<ReportingAPIResponse> ReportAsync(string token, IEvent @event, int timeoutInMS);
	}
}
