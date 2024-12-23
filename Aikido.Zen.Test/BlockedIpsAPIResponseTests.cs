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
            var response = new BlockedIpsAPIResponse
            {
                BlockedIPAddresses = new List<BlockedIpsAPIResponse.BlockedIPAddressesList>
                {
                    new BlockedIpsAPIResponse.BlockedIPAddressesList
                    {
                        Source = "source1",
                        Description = "desc1",
                        Ips = new[] { "1.1.1.1", "2.2.2.2" }
                    },
                    new BlockedIpsAPIResponse.BlockedIPAddressesList
                    {
                        Source = "source2", 
                        Description = "desc2",
                        Ips = new[] { "3.3.3.3", "4.4.4.4" }
                    }
                }
            };

            // Act
            var result = response.Ips.ToList();

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
            var response = new BlockedIpsAPIResponse
            {
                BlockedIPAddresses = new List<BlockedIpsAPIResponse.BlockedIPAddressesList>()
            };

            // Act
            var result = response.Ips;

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Ips_WithNullBlockedIpLists_ReturnsEmptyCollection()
        {
            // Arrange
            var response = new BlockedIpsAPIResponse
            {
                BlockedIPAddresses = null
            };

            // Act
            var result = response.Ips;

            // Assert
            Assert.That(result, Is.Empty);
        }
    }
}
