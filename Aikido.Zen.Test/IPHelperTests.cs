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

            //ip4 /24
            var address1 = IPAddress.Parse("192.168.1.5");
            var subnet1 = IPAddressRange.Parse("192.168.1.0/24");

            //ip4 /16
            var address2 = IPAddress.Parse("192.168.1.5");
            var subnet2 = IPAddressRange.Parse("192.168.0.0/16");

            //ip6
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

            //ip4 /24
            var address1 = IPAddress.Parse("192.168.2.5");
            var subnet1 = IPAddressRange.Parse("192.168.21.0/24");

            //ip4 /16
            var address2 = IPAddress.Parse("192.168.2.5");
            var subnet2 = IPAddressRange.Parse("192.159.21.0/16");

            //ip6
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
    }
}
