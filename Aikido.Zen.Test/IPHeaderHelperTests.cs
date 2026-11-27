using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

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
                Assert.That(
                    IPHeaderHelper.ParseIpHeader("::ffff:185.245.255.212"),
                    Is.EqualTo(new[] { "::ffff:185.245.255.212" })
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

        [TestCase("7.7.7.7, 8.8.8.8", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1, unknown,", "8.8.8.8")]
        [TestCase("2606:4700::1111, [2001:4860:4860::8888]:443, ::1", "2001:4860:4860::8888")]
        [TestCase("10.0.0.1, unknown", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void GetClientIpFromHeader_SelectsRightmostPublicIp(string? header, string? expected)
        {
            Assert.That(IPHeaderHelper.GetClientIpFromHeader(header, TrustProxyConfig.Parse(null)), Is.EqualTo(expected));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("TrUe", true)]
        [TestCase("yes", true)]
        [TestCase("YES", true)]
        [TestCase("YeS", true)]
        [TestCase("y", true)]
        [TestCase("Y", true)]
        [TestCase("on", true)]
        [TestCase("ON", true)]
        [TestCase("oN", true)]
        [TestCase("1", true)]
        [TestCase("2", true)]
        [TestCase("002", true)]
        [TestCase("2147483647", true)]
        [TestCase("false", false)]
        [TestCase("FALSE", false)]
        [TestCase("FaLsE", false)]
        [TestCase("no", false)]
        [TestCase("NO", false)]
        [TestCase("n", false)]
        [TestCase("N", false)]
        [TestCase("off", false)]
        [TestCase("OFF", false)]
        [TestCase("0", false)]
        [TestCase("-1", false)]
        [TestCase("+2", false)]
        [TestCase("2.0", true)]
        [TestCase(" 2", true)]
        [TestCase(" true ", true)]
        [TestCase(" yes ", true)]
        [TestCase(" FALSE ", false)]
        [TestCase(" ", false)]
        [TestCase("\t\r\n", false)]
        [TestCase("2147483648", false)]
        [TestCase("invalid", false)]
        public void TrustProxyConfig_Parse_RecognizesBooleanValuesAndHopCounts(string? setting, bool expected)
        {
            Assert.That(TrustProxyConfig.Parse(setting).Enabled, Is.EqualTo(expected));
        }

        [TestCase(null, null, true)]
        [TestCase("false", null, false)]
        [TestCase("2", null, true)]
        [TestCase("10.0.0.0/8", "10.1.2.3", true)]
        [TestCase("10.0.0.0/8", "8.8.8.8", false)]
        [TestCase("10.0.0.0/8", null, false)]
        [TestCase("10.0.0.0/8", "", false)]
        [TestCase("10.0.0.0/8", "invalid", false)]
        [TestCase("10.0.0.0/8", "::ffff:10.1.2.3", true)]
        [TestCase("9.9.9.9", "9.9.9.9", true)]
        [TestCase("9.9.9.9", "9.9.9.8", false)]
        [TestCase(" 10.0.0.0/8, , fd00::/8 ", "fd00::1234", true)]
        [TestCase("bad.example,10.0.0.0/8", "10.1.2.3", true)]
        [TestCase("bad.example,10.0.0.0/33", "10.1.2.3", false)]
        [TestCase("10.0.0.0/8junk", "10.1.2.3", false)]
        [TestCase("fd00::/129", "fd00::1", false)]
        [TestCase("0.0.0.0/0", "8.8.8.8", true)]
        [TestCase("0.0.0.0/0", "2001:4860::8888", false)]
        [TestCase("::/0", "2001:4860::8888", true)]
        [TestCase("::/0", "8.8.8.8", false)]
        public void TrustProxyConfig_IsTrustedProxy_ChecksConnectionIp(string? setting, string? connectionIp, bool expected)
        {
            Assert.That(TrustProxyConfig.Parse(setting).IsTrustedProxy(connectionIp), Is.EqualTo(expected));
        }

        [TestCase("8.8.8.8, 9.9.9.9", "9.9.9.9", "8.8.8.8")]
        [TestCase("8.8.8.8, 9.9.9.9, 10.1.2.3", "10.0.0.0/8,9.9.9.0/24", "8.8.8.8")]
        [TestCase("8.8.8.8, 7.7.7.7, 9.9.9.9", "9.9.9.0/24", "7.7.7.7")]
        [TestCase("8.8.8.8, unknown,, 9.9.9.9,", "9.9.9.0/24", "8.8.8.8")]
        [TestCase("2001:4860::8888, [2606:4700::1111]:443", "2606:4700::/32", "2001:4860::8888")]
        [TestCase("8.8.8.8, ::ffff:9.9.9.9", "9.9.9.0/24", "8.8.8.8")]
        [TestCase("9.9.9.9, 10.1.2.3", "9.9.9.0/24", null)]
        [TestCase("8.8.8.8", "0.0.0.0/0", null)]
        [TestCase("", "9.9.9.0/24", null)]
        [TestCase(null, "9.9.9.0/24", null)]
        public void GetClientIpFromHeader_SkipsConfiguredProxyRanges(string? header, string setting, string? expected)
        {
            Assert.That(IPHeaderHelper.GetClientIpFromHeader(header, TrustProxyConfig.Parse(setting)), Is.EqualTo(expected));
        }

        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "1", "10.0.0.1")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "2", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "3", "7.7.7.7")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "002", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", " 1 ", "10.0.0.1")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "\t2\r\n", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", " 002 ", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "4", null)]
        [TestCase("7.7.7.7, unknown, 10.0.0.1", "2", null)]
        [TestCase("7.7.7.7, , 10.0.0.1", "2", null)]
        [TestCase("7.7.7.7, 10.0.0.1,", "1", null)]
        [TestCase("127.0.0.1:443, 8.8.8.8", "2", "127.0.0.1")]
        [TestCase("2606:4700::1111, [fd00::1]:443", "1", "fd00::1")]
        [TestCase("7.7.7.7", "2147483647", null)]
        [TestCase("", "1", null)]
        [TestCase(null, "1", null)]
        public void GetClientIpFromHeader_SelectsExactHop(string? header, string trustProxy, string? expected)
        {
            Assert.That(IPHeaderHelper.GetClientIpFromHeader(header, TrustProxyConfig.Parse(trustProxy)), Is.EqualTo(expected));
        }
    }
}
