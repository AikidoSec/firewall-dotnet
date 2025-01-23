using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Ip;
using System.Net;
using System.Text.Json;

namespace Aikido.Zen.Test.Helpers
{
    public class IPHelperTests
    {


        [Test]
        public void IsValidIP_ShouldReturnTrue_WhenValidIP()
        {
            // Arrange
            var validIp1 = "192.168.1.1";
            var validIp2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

            // Act
            bool result1 = IPAddress.TryParse(validIp1, out _);
            bool result2 = IPAddress.TryParse(validIp2, out _);

            // Assert
            Assert.That(result1);
            Assert.That(result2);
        }

        [Test]
        public void IsValidIP_ShouldReturnFalse_WhenInvalidIP()
        {
            // Arrange
            var invalidIp1 = "999.999.999.999";
            var invalidIp2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334:1234";

            // Act
            bool result1 = IPAddress.TryParse(invalidIp1, out _);
            bool result2 = IPAddress.TryParse(invalidIp2, out _);

            // Assert
            Assert.That(result1, Is.False);
            Assert.That(result2, Is.False);
        }

        [Test]
        public void ToCidrString_ShouldReturnSingleCidr_WhenSingleIpProvided()
        {
            // Arrange
            var startIp = "192.168.1.1";

            // Act
            var result = IPHelper.ToCidrString(startIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.1" }));
        }

        [Test]
        public void ToCidrString_ShouldReturnCidrRange_WhenIpRangeProvided()
        {
            // Arrange
            var startIp = "192.168.1.0";
            var endIp = "192.168.1.255";

            // Act
            var result = IPHelper.ToCidrString(startIp, endIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.0/24" }));
        }

        [Test]
        public void ToCidrString_ShouldReturnMultipleCidrs_WhenNonContiguousIpRangeProvided()
        {
            // Arrange
            var startIp = "192.168.1.0";
            var endIp = "192.168.1.128";

            // Act
            var result = IPHelper.ToCidrString(startIp, endIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.0/25", "192.168.1.128/32" }));
        }
    }
}
