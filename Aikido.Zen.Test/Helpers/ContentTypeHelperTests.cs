using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ContentTypeHelperTests
    {
        // Zen must resolve a media type the way ASP.NET Core does, reading the leading type/subtype and
        // ignoring what trails it. Anything Zen fails to recognise but the framework still deserializes is
        // a body that goes uninspected.

        // Well-formed headers and the parameter section are unaffected.
        [TestCase("application/json", "application/json")]
        [TestCase("application/json; charset=utf-8", "application/json")]
        [TestCase("APPLICATION/JSON", "APPLICATION/JSON")]
        [TestCase("application/vnd.api+json", "application/vnd.api+json")]
        [TestCase("multipart/form-data; boundary=abc", "multipart/form-data")]
        // Trailing content after the media type.
        [TestCase("application/json _", "application/json")]
        [TestCase("application/json foo", "application/json")]
        [TestCase("application/json a b", "application/json")]
        [TestCase("application/json\t_", "application/json")]
        [TestCase("application/json,", "application/json")]
        [TestCase("application/json, application/json", "application/json")]
        [TestCase("application/vnd.api+json _", "application/vnd.api+json")]
        // Whitespace around the '/' - the framework tolerates it, so we do too.
        [TestCase("application /json", "application/json")]
        [TestCase("application /json _", "application/json")]
        [TestCase("application/ json _", "application/json")]
        [TestCase("application / json _", "application/json")]
        [TestCase("  application/json  ", "application/json")]
        // Characters RFC 7230 excludes from a token end the media type just as whitespace does.
        [TestCase("application/json\"x", "application/json")]
        [TestCase("application/json(x)", "application/json")]
        [TestCase("application/json<x", "application/json")]
        [TestCase("application/json>x", "application/json")]
        [TestCase("application/json@x", "application/json")]
        [TestCase("application/json[x]", "application/json")]
        [TestCase("application/json\\x", "application/json")]
        [TestCase("application/json?x", "application/json")]
        [TestCase("application/json=x", "application/json")]
        [TestCase("application/json:x", "application/json")]
        [TestCase("application/json{x", "application/json")]
        // Not a media type at all - must not be coerced into one.
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase("nonsense", "")]
        [TestCase("application/", "")]
        [TestCase("/json", "")]
        [TestCase("/", "")]
        [TestCase("; charset=utf-8", "")]
        // Distinct media types must stay distinct.
        [TestCase("application/jsonp", "application/jsonp")]
        [TestCase("application/notjson", "application/notjson")]
        [TestCase("application/json-patch", "application/json-patch")]
        [TestCase("text/html", "text/html")]
        public void GetMediaType_ReturnsLeadingTypeSubtype(string contentType, string expected)
        {
            Assert.That(ContentTypeHelper.GetMediaType(contentType), Is.EqualTo(expected));
        }

        [Test]
        public void GetMediaType_HandlesNull()
        {
            Assert.That(ContentTypeHelper.GetMediaType(null), Is.EqualTo(string.Empty));
        }

        [TestCase("text/plain; charset=utf-16", "charset", "utf-16")]
        [TestCase("text/plain; CHARSET=utf-16", "charset", "utf-16")]
        [TestCase("text/plain; charset=\"utf-16\"", "charset", "utf-16")]
        [TestCase("text/plain;charset=utf-16", "charset", "utf-16")]
        [TestCase("text/plain; charset = utf-16", "charset", "utf-16")]
        [TestCase("text/plain; charset=utf-16; format=flowed", "charset", "utf-16")]
        [TestCase("text/plain; format=flowed; charset=utf-16", "charset", "utf-16")]
        // Malformed text after the media type must not hide the parameters.
        [TestCase("application/json _; charset=utf-16", "charset", "utf-16")]
        // A ';' inside a quoted value does not start a new parameter.
        [TestCase("multipart/form-data; note=\"a;b\"; boundary=xyz", "boundary", "xyz")]
        [TestCase("multipart/form-data; note=\"a;boundary=evil\"; boundary=xyz", "boundary", "xyz")]
        [TestCase("multipart/form-data; boundary=\"a;b\"", "boundary", "a;b")]
        [TestCase("multipart/form-data; boundary=\"a\\\"b\"", "boundary", "a\"b")]
        [TestCase("text/plain; charset=\"\"", "charset", "")]
        [TestCase("text/plain", "charset", null)]
        [TestCase("text/plain;", "charset", null)]
        [TestCase("text/plain; charset", "charset", null)]
        [TestCase("text/plain; =utf-16", "charset", null)]
        [TestCase("text/plain; xcharset=utf-16", "charset", null)]
        [TestCase("", "charset", null)]
        [TestCase(null, "charset", null)]
        public void GetParameter_ReturnsUnquotedValue(string? contentType, string name, string? expected)
        {
            Assert.That(ContentTypeHelper.GetParameter(contentType!, name), Is.EqualTo(expected));
        }

        [TestCase("multipart/form-data; boundary=abc", "abc")]
        [TestCase("multipart/form-data;boundary=abc", "abc")]
        [TestCase("multipart/form-data; boundary=\"abc\"", "abc")]
        [TestCase("MULTIPART/FORM-DATA; BOUNDARY=abc", "abc")]
        [TestCase("multipart/form-data; charset=utf-8; boundary=abc", "abc")]
        [TestCase("multipart/form-data; boundary=\"a b\"", "a b")]
        public void TryGetMultipartBoundary_ReturnsBoundary(string contentType, string expected)
        {
            Assert.That(ContentTypeHelper.TryGetMultipartBoundary(contentType, out var boundary), Is.True);
            Assert.That(boundary, Is.EqualTo(expected));
        }

        [TestCase("multipart/form-data")]
        [TestCase("multipart/form-data;")]
        [TestCase("multipart/form-data; boundary=")]
        [TestCase("multipart/form-data; boundary=\"\"")]
        [TestCase("multipart/mixed; boundary=abc")]
        [TestCase("application/json; boundary=abc")]
        [TestCase("")]
        [TestCase(null)]
        public void TryGetMultipartBoundary_ReturnsFalseWithoutMultipartBoundary(string? contentType)
        {
            Assert.That(ContentTypeHelper.TryGetMultipartBoundary(contentType!, out var boundary), Is.False);
            Assert.That(boundary, Is.Null.Or.Empty);
        }
    }
}