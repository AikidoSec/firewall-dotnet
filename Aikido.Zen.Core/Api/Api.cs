using System.Text.Json;

namespace Aikido.Zen.Core.Api
{
    public class ZenApi : IZenApi
    {
        public static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };
        public ZenApi(IReportingAPIClient reporting, IRuntimeAPIClient runtime)
        {
            Reporting = reporting;
            Runtime = runtime;
        }
        public IReportingAPIClient Reporting { get; private set; }
        public IRuntimeAPIClient Runtime { get; private set; }
    }
}
