using Aikido.Zen.Core.Api;
using Moq;

namespace Aikido.Zen.Test.Mocks
{
    public static class ZenApiMock
    {
        public static Mock<IZenApi> CreateMock (IReportingAPIClient reporting = null, IRuntimeAPIClient runtime = null)
        {
            if (reporting == null)
            {
                var reportingMock = new Mock<IReportingAPIClient>();
                reportingMock
                    .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                    .ReturnsAsync(new ReportingAPIResponse { Success = true });

                reportingMock
                    .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                    .ReturnsAsync(new FirewallListsAPIResponse { Success = true });
                reporting = reportingMock.Object;
            }


            if (runtime == null)
            {
                var runtimeMock = new Mock<IRuntimeAPIClient>();
                runtimeMock
                    .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });

                runtimeMock
                    .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
                runtime = runtimeMock.Object;
            }
            var zenApi = new Mock<IZenApi>();
            zenApi.Setup(z => z.Reporting).Returns(reporting);
            zenApi.Setup(z => z.Runtime).Returns(runtime);

            return zenApi;
        }

        public static Mock<IZenApi> CreateMockWithFailedResponses ()
        {
            var reportingApiClient = new Mock<IReportingAPIClient>();
            reportingApiClient
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = false, Error = "Test error" });

            reportingApiClient
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = false, Error = "Test error" });

            var runtimeApiClient = new Mock<IRuntimeAPIClient>();
            runtimeApiClient
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = false, Error = "Test error" });

            runtimeApiClient
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = false, Error = "Test error" });

            var zenApi = new Mock<IZenApi>();
            zenApi.Setup(z => z.Reporting).Returns(reportingApiClient.Object);
            zenApi.Setup(z => z.Runtime).Returns(runtimeApiClient.Object);

            return zenApi;
        }

        public static Mock<IZenApi> CreateMockWithExceptions ()
        {
            var reportingApiClient = new Mock<IReportingAPIClient>();
            reportingApiClient
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                    .ThrowsAsync(new System.Exception("Test exception"));

            reportingApiClient
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("Test exception"));

            var runtimeApiClient = new Mock<IRuntimeAPIClient>();
            runtimeApiClient
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("Test exception"));

            runtimeApiClient
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("Test exception"));

            var zenApi = new Mock<IZenApi>();
            zenApi.Setup(z => z.Reporting).Returns(reportingApiClient.Object);
            zenApi.Setup(z => z.Runtime).Returns(runtimeApiClient.Object);

            return zenApi;
        }
    }
}
