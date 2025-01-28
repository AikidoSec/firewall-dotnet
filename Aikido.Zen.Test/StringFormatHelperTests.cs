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
        public void GetStringFormat_WithInvalidString_ReturnsNull()
        {
            var result = OpenAPIHelper.GetStringFormat("invalid");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetStringFormat_WithUnknownFormat_ReturnsNull()
        {
            Assert.That(OpenAPIHelper.GetStringFormat(""), Is.Null);
            Assert.That(OpenAPIHelper.GetStringFormat("abc"), Is.Null);
            Assert.That(OpenAPIHelper.GetStringFormat("2021-11-25T"), Is.Null);
            Assert.That(OpenAPIHelper.GetStringFormat("test".PadLeft(64, 't')), Is.Null);
        }

        [Test]
        public void GetStringFormat_WithDateString_ReturnsDateFormat()
        {
            Assert.That(OpenAPIHelper.GetStringFormat("2021-01-01"), Is.EqualTo("date"));
            Assert.That(OpenAPIHelper.GetStringFormat("2021-12-31"), Is.EqualTo("date"));
        }

        [Test]
        public void GetStringFormat_WithDateTimeString_ReturnsDateTimeFormat()
        {
            Assert.That(OpenAPIHelper.GetStringFormat("1985-04-12T23:20:50.52Z"), Is.EqualTo("date-time"));
            Assert.That(OpenAPIHelper.GetStringFormat("1996-12-19T16:39:57-08:00"), Is.EqualTo("date-time"));
            Assert.That(OpenAPIHelper.GetStringFormat("1990-12-31T23:59:60Z"), Is.EqualTo("date-time"));
            Assert.That(OpenAPIHelper.GetStringFormat("1990-12-31T15:59:60-08:00"), Is.EqualTo("date-time"));
            Assert.That(OpenAPIHelper.GetStringFormat("1937-01-01T12:00:27.87+00:20"), Is.EqualTo("date-time"));
        }

        [Test]
        public void GetStringFormat_WithUuidString_ReturnsUuidFormat()
        {
            Assert.That(OpenAPIHelper.GetStringFormat("550e8400-e29b-41d4-a716-446655440000"), Is.EqualTo("uuid"));
            Assert.That(OpenAPIHelper.GetStringFormat("00000000-0000-0000-0000-000000000000"), Is.EqualTo("uuid"));
        }

        [Test]
        public void GetStringFormat_WithEmailString_ReturnsEmailFormat()
        {
            Assert.That(OpenAPIHelper.GetStringFormat("hello@example.com"), Is.EqualTo("email"));
            Assert.That(OpenAPIHelper.GetStringFormat("ö@ö.de"), Is.EqualTo("email"));
        }

        [Test]
        public void GetStringFormat_WithUriString_ReturnsUriFormat()
        {
            Assert.That(OpenAPIHelper.GetStringFormat("http://example.com"), Is.EqualTo("uri"));
            Assert.That(OpenAPIHelper.GetStringFormat("https://example.com"), Is.EqualTo("uri"));
            Assert.That(OpenAPIHelper.GetStringFormat("ftp://example.com"), Is.EqualTo("uri"));
        }
    }
}
