using Aikido.Zen.Core.Api;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Test
{
    public class BlockedIpsAPIResponseTests
    {
        [Test]
        public void Ips_WithMultipleBlockedIpLists_ReturnsAllIps()
        {
            // Arrange
            var response = new FirewallListsAPIResponse
            {
                BlockedIPAddresses = new List<FirewallListsAPIResponse.IPList>
                {
                    new FirewallListsAPIResponse.IPList
                    {
                        Source = "source1",
                        Description = "desc1",
                        Ips = new[] { "1.1.1.1", "2.2.2.2" }
                    },
                    new FirewallListsAPIResponse.IPList
                    {
                        Source = "source2",
                        Description = "desc2",
                        Ips = new[] { "3.3.3.3", "4.4.4.4" }
                    }
                }
            };

            // Act
            var result = response.BlockedIps.ToList();

            // Assert
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result, Contains.Item("1.1.1.1"));
            Assert.That(result, Contains.Item("2.2.2.2"));
            Assert.That(result, Contains.Item("3.3.3.3"));
            Assert.That(result, Contains.Item("4.4.4.4"));
        }

        [Test]
        public void Ips_WithEmptyBlockedIpLists_ReturnsEmptyCollection()
        {
            // Arrange
            var response = new FirewallListsAPIResponse
            {
                BlockedIPAddresses = new List<FirewallListsAPIResponse.IPList>()
            };

            // Act
            var result = response.BlockedIps;

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Ips_WithNullBlockedIpLists_ReturnsEmptyCollection()
        {
            // Arrange
            var response = new FirewallListsAPIResponse
            {
                BlockedIPAddresses = null
            };

            // Act
            var result = response.BlockedIps;

            // Assert
            Assert.That(result, Is.Empty);
        }
    }
}
