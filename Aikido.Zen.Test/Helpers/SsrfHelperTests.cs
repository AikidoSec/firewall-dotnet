using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System.Net;
using System.Linq;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class SsrfHelperTests
    {
        // Helper to get IP addresses for a hostname, handling potential exceptions
        private IPAddress[] GetAddresses(string hostname)
        {
            if (string.IsNullOrEmpty(hostname) || hostname == "localhost")
            {
                // Consistent handling for localhost as in SsrfHelper
                try
                {
                    return Dns.GetHostAddresses(Dns.GetHostName())
                                   .Concat(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
                                   .Distinct()
                                   .ToArray();
                }
                catch
                {
                    return new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }; // Fallback if GetHostName fails
                }
            }
            if (IPAddress.TryParse(hostname, out var ip))
            {
                return new[] { ip };
            }
            try
            {
                return Dns.GetHostAddresses(hostname);
            }
            catch
            {
                return System.Array.Empty<IPAddress>(); // Return empty if resolution fails
            }
        }

        [Test]
        public void FindHostname_ReturnsFalse_WhenUserInputAndHostnameAreEmpty()
        {
            Assert.That(SsrfHelper.FindHostnameInUserInput("", "", GetAddresses(""), null), Is.False);
        }

        [Test]
        public void FindHostname_ReturnsFalse_WhenUserInputIsEmpty()
        {
            var hostname = "example.com";
            Assert.That(SsrfHelper.FindHostnameInUserInput("", hostname, GetAddresses(hostname), null), Is.False);
        }

        [Test]
        public void FindHostname_ReturnsFalse_WhenHostnameIsEmpty()
        {
            // Note: The GetAddresses helper will return empty for an empty hostname.
            // The TryParseURL logic might still parse "http://example.com", but hostname matching should fail.
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://example.com", "", GetAddresses(""), null), Is.False);
        }

        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_SimpleHttp()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_SimpleHttps()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("https://localhost", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_NoScheme()
        {
            var hostname = "localhost";
            // TryParseUrl adds http://
            Assert.That(SsrfHelper.FindHostnameInUserInput("localhost", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_WithTrailingPath()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost/path", hostname, GetAddresses(hostname), null), Is.True);
        }


        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_NoSchemeWithTrailingPath()
        {
            var hostname = "localhost";
            // TryParseUrl adds http://
            Assert.That(SsrfHelper.FindHostnameInUserInput("localhost/path/path", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_ParsesHostnameFromUserInput_FtpScheme()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("ftp://localhost", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_IgnoresInvalidUrls()
        {
            var hostname = "localhost";
            // Uri.TryCreate fails for "http://"
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://", hostname, GetAddresses(hostname), null), Is.False);
            Assert.That(SsrfHelper.FindHostnameInUserInput("://", hostname, GetAddresses(hostname), null), Is.False);
            Assert.That(SsrfHelper.FindHostnameInUserInput(" some invalid string ", hostname, GetAddresses(hostname), null), Is.False);
        }

        [Test]
        public void FindHostname_ReturnsFalse_WhenUserInputIsSubstringOfHostname()
        {
            // This case seems unlikely to be a real SSRF vector targeted by this check,
            // as the user input doesn't parse to the target host.
            var userInput = "localhost";
            var hostname = "localhost localhost"; // Hostname itself is invalid, GetAddresses likely empty
            Assert.That(SsrfHelper.FindHostnameInUserInput(userInput, hostname, GetAddresses(hostname), null), Is.False);
        }

        [Test]
        public void FindHostname_FindsIpAddressInsideUrl()
        {
            var ip = "169.254.169.254";
            Assert.That(SsrfHelper.FindHostnameInUserInput($"http://{ip}/latest/meta-data/", ip, GetAddresses(ip), null), Is.True);
        }

        // Note: C# Uri parsing does not support direct decimal/octal IP formats like JS might.
        // Test cases like "http://2130706433" or "http://127.1" will likely have Uri.Host resolve to the literal string,
        // not the parsed IP. The check might still pass if the targetHostname is *also* the literal string,
        // or if DNS resolves the string literal back to an expected IP (unlikely for these examples).
        // We test the direct string match case.
        [Test]
        public void FindHostname_FindsDecimalIpAsStringInsideUrl()
        {
            var ipString = "2130706433";
            // We pass the string literal as hostname. GetAddresses will fail DNS, but direct string comparison works.
            Assert.That(SsrfHelper.FindHostnameInUserInput($"http://{ipString}", ipString, GetAddresses(ipString), null), Is.True);
        }

        [Test]
        public void FindHostname_FindsShortIpAsStringInsideUrl()
        {
            var ipString = "127.1";
            Assert.That(SsrfHelper.FindHostnameInUserInput($"http://{ipString}", ipString, GetAddresses(ipString), null), Is.True);
        }

        [Test]
        public void FindHostname_FindsShortIpWithZeroAsStringInsideUrl()
        {
            var ipString = "127.0.1";
            Assert.That(SsrfHelper.FindHostnameInUserInput($"http://{ipString}", ipString, GetAddresses(ipString), null), Is.True);
        }


        [Test]
        public void FindHostname_WorksWithPorts_NoMatchPortSpecifiedInInputOnly()
        {
            var hostname = "localhost";
            // Target port 8080 specified, input implies default (80 for http)
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost", hostname, GetAddresses(hostname), 8080), Is.False);
        }

        [Test]
        public void FindHostname_WorksWithPorts_MatchPortSpecifiedInBoth()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost:8080", hostname, GetAddresses(hostname), 8080), Is.True);
        }

        [Test]
        public void FindHostname_WorksWithPorts_MatchNoPortSpecifiedInTarget()
        {
            var hostname = "localhost";
            // Target port null (don't care), input has 8080. Should match hostname.
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost:8080", hostname, GetAddresses(hostname), null), Is.True);
        }

        [Test]
        public void FindHostname_WorksWithPorts_NoMatchDifferentPortsSpecified()
        {
            var hostname = "localhost";
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://localhost:8080", hostname, GetAddresses(hostname), 4321), Is.False);
        }

        [Test]
        public void FindHostname_WorksWithDefaultPorts_Http()
        {
            var hostname = "example.com";
            // Input implies port 80, target explicitly port 80
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://example.com", hostname, GetAddresses(hostname), 80), Is.True);
        }

        [Test]
        public void FindHostname_WorksWithDefaultPorts_Https()
        {
            var hostname = "example.com";
            // Input implies port 443, target explicitly port 443
            Assert.That(SsrfHelper.FindHostnameInUserInput("https://example.com", hostname, GetAddresses(hostname), 443), Is.True);
        }

        [Test]
        public void FindHostname_WorksWithDefaultPorts_NoMatchHttpInputHttpsTargetPort()
        {
            var hostname = "example.com";
            Assert.That(SsrfHelper.FindHostnameInUserInput("http://example.com", hostname, GetAddresses(hostname), 443), Is.False);
        }

        [Test]
        public void FindHostname_WorksWithDefaultPorts_NoMatchHttpsInputHttpTargetPort()
        {
            var hostname = "example.com";
            Assert.That(SsrfHelper.FindHostnameInUserInput("https://example.com", hostname, GetAddresses(hostname), 80), Is.False);
        }

        [Test]
        public void FindHostname_ResolvesHostnameInInput_MatchesTargetIp()
        {
            var targetIp = "127.0.0.1"; // Use a known IP
            var hostnameInInput = "localhost"; // This should resolve to 127.0.0.1
            var targetAddresses = GetAddresses(targetIp);
            // Expect true because localhost resolves to an IP contained in targetAddresses (127.0.0.1)
            Assert.That(SsrfHelper.FindHostnameInUserInput(hostnameInInput, targetIp, targetAddresses, null), Is.True);
        }

        [Test]
        public void FindHostname_ResolvesHostnameInTarget_MatchesInputIp()
        {
            var targetHostname = "localhost";
            var inputIp = "127.0.0.1";
            var targetAddresses = GetAddresses(targetHostname);
            // Expect true because targetAddresses contains 127.0.0.1 which matches the parsed input IP
            Assert.That(SsrfHelper.FindHostnameInUserInput(inputIp, targetHostname, targetAddresses, null), Is.True);
        }
    }
}
