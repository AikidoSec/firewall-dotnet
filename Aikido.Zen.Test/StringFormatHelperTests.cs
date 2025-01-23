using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class StringFormatHelperTests
    {
        [Test]
        public void GetStringFormat_WithValidEmail_ReturnsEmailFormat()
        {
            var result = OpenAPIHelper.GetStringFormat("test@example.com");
            Assert.That(result, Is.EqualTo("email"));
        }

        [Test]
        public void GetStringFormat_WithValidDate_ReturnsDateFormat()
        {
            var result = OpenAPIHelper.GetStringFormat("2021-01-01");
            Assert.That(result, Is.EqualTo("date"));
        }

        [Test]
        public void GetStringFormat_WithValidDateTime_ReturnsDateTimeFormat()
        {
            var result = OpenAPIHelper.GetStringFormat("2021-01-01T12:00:00Z");
            Assert.That(result, Is.EqualTo("date-time"));
        }

        [Test]
        public void GetStringFormat_WithValidUuid_ReturnsUuidFormat()
        {
            var result = OpenAPIHelper.GetStringFormat("550e8400-e29b-41d4-a716-446655440000");
            Assert.That(result, Is.EqualTo("uuid"));
        }

        [Test]
        public void GetStringFormat_WithValidIPv4_ReturnsIpv4Format()
        {
            var result = OpenAPIHelper.GetStringFormat("192.168.1.1");
            Assert.That(result, Is.EqualTo("ipv4"));
        }

        [Test]
        public void GetStringFormat_WithInvalidString_ReturnsNull()
        {
            var result = OpenAPIHelper.GetStringFormat("invalid");
            Assert.That(result, Is.Null);
        }
    }
}
