namespace Aikido.Zen.Core.Api
{
	public interface IZenApi
	{
		IReportingAPIClient Reporting { get; }
        IRuntimeAPIClient Runtime { get; }
    }
}
