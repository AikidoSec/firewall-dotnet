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
            Assert.That(IPHeaderHelper.GetClientIpFromHeader(header), Is.EqualTo(expected));
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
        [TestCase("2.0", false)]
        [TestCase(" 2", false)]
        [TestCase(" true ", false)]
        [TestCase("2147483648", false)]
        [TestCase("invalid", false)]
        public void ShouldTrustProxy_RecognizesBooleanValuesAndHopCounts(string? setting, bool expected)
        {
            Assert.That(IPHeaderHelper.ShouldTrustProxy(setting), Is.EqualTo(expected));
        }

        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "1", "10.0.0.1")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "2", "8.8.8.8")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "3", "7.7.7.7")]
        [TestCase("7.7.7.7, 8.8.8.8, 10.0.0.1", "002", "8.8.8.8")]
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
            Assert.That(IPHeaderHelper.GetClientIpFromHeader(header, trustProxy), Is.EqualTo(expected));
        }
    }
}
