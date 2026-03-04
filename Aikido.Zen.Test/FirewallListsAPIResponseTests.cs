using Aikido.Zen.Core.Api;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class FirewallListsAPIResponseTests
    {
        [Test]
        public void FirewallListsAPIResponse_Defaults_AreInitialized()
        {
            var response = new FirewallListsAPIResponse();

            Assert.That(response.BlockedIPAddresses, Is.Not.Null);
            Assert.That(response.AllowedIPAddresses, Is.Not.Null);
            Assert.That(response.MonitoredIPAddresses, Is.Not.Null);
            Assert.That(response.UserAgentDetails, Is.Not.Null);
            Assert.That(response.BlockedUserAgents, Is.EqualTo(string.Empty));
            Assert.That(response.MonitoredUserAgents, Is.EqualTo(string.Empty));
        }
    }
}
