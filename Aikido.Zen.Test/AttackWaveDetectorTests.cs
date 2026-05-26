using Aikido.Zen.Core;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class AttackWaveDetectorTests
    {
        private static AttackWaveDetector NewDetector(
            int threshold = 2,
            int timeframe = 200,
            int minBetweenEvents = 300,
            int maxSamples = 5)
        {
            return new AttackWaveDetector(new AttackWaveDetectorOptions
            {
                AttackWaveThreshold = threshold,
                AttackWaveTimeFrame = timeframe,
                MinTimeBetweenEvents = minBetweenEvents,
                MaxSamplesPerIP = maxSamples,
                MaxLRUEntries = 1000,
            });
        }

        [Test]
        public void Check_NoIp_ReturnsFalse()
        {
            var detector = NewDetector();
            var context = BuildContext(string.Empty, "/wp-config.php", "GET");

            Assert.That(detector.Check(context, 404), Is.False);
        }

        [Test]
        public async Task Check_DetectsWave_WhenThresholdExceeded()
        {
            var detector = NewDetector();
            var context = BuildContext("::1", "/wp-config.php", "GET");

            Assert.That(detector.Check(context, 404), Is.False);
            Assert.That(detector.Check(context, 404), Is.True);
            Assert.That(detector.Check(context, 404), Is.False); // event already sent

            await Task.Delay(400); // allow both the timeframe and minTimeBetweenEvents to expire

            Assert.That(detector.Check(context, 404), Is.False);
            Assert.That(detector.Check(context, 404), Is.True);
        }

        [Test]
        public void Check_DetectsHighConfidenceProbe()
        {
            var detector = NewDetector();
            var context = BuildContext("::1", "/wp-config.php", "GET");

            Assert.That(detector.Check(context, 200), Is.False);
            Assert.That(detector.Check(context, 200), Is.True);
        }

        [TestCase("/random.php")]
        [TestCase("/random.java")]
        [TestCase("/random.jsp")]
        public void Check_IgnoresStatusSensitiveFileExtensionProbe_WhenStatusIsSuccessful(string url)
        {
            var detector = NewDetector();
            var context = BuildContext("::1", url, "GET");

            Assert.That(detector.Check(context, 200), Is.False);
            Assert.That(detector.Check(context, 302), Is.False);
            Assert.That(detector.GetSamplesForIp("::1"), Is.Empty);
        }

        [TestCase(400)]
        [TestCase(404)]
        [TestCase(500)]
        public void Check_DetectsStatusSensitiveFileExtensionProbe_WhenStatusIsNotSuccessful(int statusCode)
        {
            var detector = NewDetector();
            var context = BuildContext("::1", "/random.php", "GET");

            Assert.That(detector.Check(context, statusCode), Is.False);
            Assert.That(detector.Check(context, statusCode), Is.True);
        }

        [Test]
        public void TrackSample_StoresUniqueSamplesWithinLimit()
        {
            var detector = NewDetector(threshold: 10, timeframe: 1000, minBetweenEvents: 1000, maxSamples: 3);
            var ip = "::1";

            detector.Check(BuildContext(ip, "/0/.env", "GET"), 404);
            detector.Check(BuildContext(ip, "/1/.env", "GET"), 404);
            detector.Check(BuildContext(ip, "/2/.env", "GET"), 404);
            detector.Check(BuildContext(ip, "/2/.env", "GET"), 404); // duplicate
            detector.Check(BuildContext(ip, "/3/.env", "GET"), 404); // should be ignored due to max samples

            var samples = detector.GetSamplesForIp(ip);

            Assert.That(samples.Count, Is.EqualTo(3));
            Assert.That(
                samples.Select(s => s.Url),
                Is.EqualTo(new[] { "/0/.env", "/1/.env", "/2/.env" }));
        }

        [Test]
        public void IsProbeRequest_DetectsDangerousSignals()
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/.git/config", "GET"), 200),
                Is.True);

            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/api", "BADMETHOD"), 200),
                Is.True);

            var contextWithQuery = BuildContext("::1", "/api", "GET", new Dictionary<string, string>
            {
                { "test", "SELECT * FROM admin" }
            });
            Assert.That(AttackWaveProbe.IsProbeRequest(contextWithQuery, 200), Is.True);
        }

        [TestCase("/wp-config.php?foo=bar")]
        [TestCase("https://example.com/wp-config.php?foo=bar")]
        [TestCase("/backup.sql?foo=bar")]
        [TestCase("https://example.com/backup.sql?foo=bar")]
        public void IsProbeRequest_DetectsSuspiciousPath_WhenUrlContainsQueryString(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 200),
                Is.True);
        }

        [TestCase("/random.php")]
        [TestCase("/random.php?foo=bar")]
        [TestCase("/random.php3")]
        [TestCase("/random.php4?foo=bar")]
        [TestCase("/random.php5")]
        [TestCase("/random.phtml")]
        [TestCase("/nested/random.PHP")]
        public void IsProbeRequest_DetectsPhpFileExtensions_WhenStatusIsNotSuccessful(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.True);
        }

        [TestCase("/random.java")]
        [TestCase("/random.java?foo=bar")]
        [TestCase("/nested/random.JAVA")]
        public void IsProbeRequest_DetectsJavaFileExtension_WhenStatusIsNotSuccessful(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.True);
        }

        [TestCase("/random.jsp")]
        [TestCase("/random.jspx?foo=bar")]
        [TestCase("/nested/random.JSP")]
        public void IsProbeRequest_DetectsJspFileExtensions_WhenStatusIsNotSuccessful(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.True);
        }

        [TestCase("/api/php/whatever")]
        [TestCase("/api/php/whatever?foo=bar")]
        [TestCase("/api/php")]
        public void IsProbeRequest_IgnoresPhpPathSegmentWithoutPhpFileExtension(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.False);
        }

        [TestCase("/api/java/whatever")]
        [TestCase("/api/java/whatever?foo=bar")]
        [TestCase("/api/java")]
        public void IsProbeRequest_IgnoresJavaPathSegmentWithoutJavaFileExtension(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.False);
        }

        [TestCase("/api/jsp/whatever")]
        [TestCase("/api/jsp/whatever?foo=bar")]
        [TestCase("/api/jspx")]
        public void IsProbeRequest_IgnoresJspPathSegmentWithoutJspFileExtension(string url)
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", url, "GET"), 404),
                Is.False);
        }

        [Test]
        public void IsProbeRequest_IgnoresBenignTraffic()
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/api/users", "GET"), 404),
                Is.False);

            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/static/js/app.js", "GET"), 404),
                Is.False);
        }

        private static Context BuildContext(string ip, string path, string method, IDictionary<string, string>? query = null)
        {
            return new Context
            {
                RemoteAddress = ip,
                Route = path,
                Url = path,
                Method = method,
                Query = query ?? new Dictionary<string, string>(),
                Headers = new Dictionary<string, string>(),
                Cookies = new Dictionary<string, string>(),
                Source = "test",
            };
        }
    }
}
