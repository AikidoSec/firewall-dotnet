using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Ip;
using NetTools;
using System.Net;
using System.Text.Json;

namespace Aikido.Zen.Test
{
    public class IPHelperTests
    {
        [Test]
        public void IsInSubnet_ShouldReturnTrue_WhenIpIsInSubnet()
        {
            // Arrange

            // IPv4 /24
            var address1 = IPAddress.Parse("192.168.1.5");
            var subnet1 = IPAddressRange.Parse("192.168.1.0/24");

            // IPv4 /16
            var address2 = IPAddress.Parse("192.168.1.5");
            var subnet2 = IPAddressRange.Parse("192.168.0.0/16");

            // IPv6
            var address3 = IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334");
            var subnet3 = IPAddressRange.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334/128");

            // Act
            bool result1 = IPHelper.IsInSubnet(address1, subnet1);
            bool result2 = IPHelper.IsInSubnet(address2, subnet2);
            bool result3 = IPHelper.IsInSubnet(address3, subnet3);

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
        }

        [Test]
        public void IsInSubnet_ShouldReturnFalse_WhenIpIsNotInSubnet()
        {
            // Arrange

            // IPv4 /24
            var address1 = IPAddress.Parse("192.168.2.5");
            var subnet1 = IPAddressRange.Parse("192.168.21.0/24");

            // IPv4 /16
            var address2 = IPAddress.Parse("192.168.2.5");
            var subnet2 = IPAddressRange.Parse("192.159.21.0/16");

            // IPv6
            var address3 = IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7335");
            var subnet3 = IPAddressRange.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334");

            // Act
            bool result1 = IPHelper.IsInSubnet(address1, subnet1);
            bool result2 = IPHelper.IsInSubnet(address2, subnet2);
            bool result3 = IPHelper.IsInSubnet(address3, subnet3);

            // Assert
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
            Assert.IsFalse(result3);
        }

        [Test]
        public void IsSubnet_ShouldReturnTrue_WhenValidCIDRNotation()
        {
            // Arrange
            var validCidr1 = "192.168.1.0/24";
            var validCidr2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334/128";

            // Act
            bool result1 = IPHelper.IsSubnet(validCidr1);
            bool result2 = IPHelper.IsSubnet(validCidr2);

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
        }

        [Test]
        public void IsSubnet_ShouldReturnFalse_WhenInvalidCIDRNotation()
        {
            // Arrange
            var invalidCidr1 = "192.168.1.0";
            var invalidCidr2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

            // Act
            bool result1 = IPHelper.IsSubnet(invalidCidr1);
            bool result2 = IPHelper.IsSubnet(invalidCidr2);

            // Assert
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
        }

        [Test]
        public void IsInSubnet_ShouldHandleInvalidIPAddress()
        {
            // Arrange
            var invalidAddress = "999.999.999.999";
            var subnet = IPAddressRange.Parse("192.168.1.0/24");

            // Act & Assert
            Assert.Throws<FormatException>(() => IPHelper.IsInSubnet(IPAddress.Parse(invalidAddress), subnet));
        }

        [Test]
        public void IsInSubnet_ShouldHandleNetworkRangeCalculations()
        {
            // Arrange
            var address = IPAddress.Parse("192.168.1.5");
            var subnet = IPAddressRange.Parse("192.168.1.0/24");

            // Act
            bool result = IPHelper.IsInSubnet(address, subnet);

            // Assert
            Assert.IsTrue(result);
        }

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
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
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
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
        }
    }
}
