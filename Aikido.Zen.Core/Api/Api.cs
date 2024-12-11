namespace Aikido.Zen.Core.Api
{
	public class ZenApi : IZenApi
	{
		public ZenApi(IReportingAPIClient reporting, IRuntimeAPIClient runtime)
		{
			Reporting = reporting;
            Runtime = runtime;
		}
		public IReportingAPIClient Reporting { get; private set; }
        public IRuntimeAPIClient Runtime { get; private set; }
    }
}
