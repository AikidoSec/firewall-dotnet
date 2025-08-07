namespace Aikido.Zen.Core.Api
{
    internal interface IZenApi
	{
		IReportingAPIClient Reporting { get; }
        IRuntimeAPIClient Runtime { get; }
    }
}
