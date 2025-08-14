using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    /// <summary>
    /// Contains unit tests for IPHeaderHelper methods.
    /// </summary>
    public class IPHeader
    {
        [Test]
        public void Parse_Valid_Headers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("127.0.0.0"),
                    Is.EqualTo(new[] { "127.0.0.0" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("127.0.0.0, 1.2.3.4"),
                    Is.EqualTo(new[] { "127.0.0.0", "1.2.3.4" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("::1, 2001:db8::1 "),
                    Is.EqualTo(new[] { "::1", "2001:db8::1" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("::1, [2001:db8::1] "),
                    Is.EqualTo(new[] { "::1", "2001:db8::1" })
                );
            });
        }

        [Test]
        public void Parse_Valid_Headers_With_Port_Numbers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("127.0.0.0:8080"),
                    Is.EqualTo(new[] { "127.0.0.0" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("127.0.0.0, 1.2.3.4:443"),
                    Is.EqualTo(new[] { "127.0.0.0", "1.2.3.4" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("[::1]:8080, 2001:db8::1 "),
                    Is.EqualTo(new[] { "::1", "2001:db8::1" })
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("[a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880]:8080, 2001:db8::1 "),
                    Is.EqualTo(new[] { "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880", "2001:db8::1" })
                );
            });
        }

        [Test]
        public void Parse_Invalid_Headers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    IPHeaderHelper.ParseIpHeader(""),
                    Is.EqualTo(Array.Empty<string>())
                );
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("-"),
                    Is.EqualTo(new[] { "-" })
                );
                Assert.That(
                   IPHeaderHelper.ParseIpHeader("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880:80, "),
                   Is.EqualTo(new[] { "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880:80" })
               );
            });
        }
    }
}
