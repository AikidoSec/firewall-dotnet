using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;
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

            Assert.That(detector.Check(context), Is.False);
        }

        [Test]
        public async Task Check_DetectsWave_WhenThresholdExceeded()
        {
            var detector = NewDetector();
            var context = BuildContext("::1", "/wp-config.php", "GET");

            Assert.That(detector.Check(context), Is.False);
            Assert.That(detector.Check(context), Is.True);
            Assert.That(detector.Check(context), Is.False); // event already sent

            await Task.Delay(400); // allow both the timeframe and minTimeBetweenEvents to expire

            Assert.That(detector.Check(context), Is.False);
            Assert.That(detector.Check(context), Is.True);
        }

        [Test]
        public void TrackSample_StoresUniqueSamplesWithinLimit()
        {
            var detector = NewDetector(threshold: 10, timeframe: 1000, minBetweenEvents: 1000, maxSamples: 3);
            var ip = "::1";

            detector.Check(BuildContext(ip, "/0/.env", "GET"));
            detector.Check(BuildContext(ip, "/1/.env", "GET"));
            detector.Check(BuildContext(ip, "/2/.env", "GET"));
            detector.Check(BuildContext(ip, "/2/.env", "GET")); // duplicate
            detector.Check(BuildContext(ip, "/3/.env", "GET")); // should be ignored due to max samples

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
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/.git/config", "GET")),
                Is.True);

            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/api", "BADMETHOD")),
                Is.True);

            var contextWithQuery = BuildContext("::1", "/api", "GET", new Dictionary<string, string>
            {
                { "test", "SELECT * FROM admin" }
            });
            Assert.That(AttackWaveProbe.IsProbeRequest(contextWithQuery), Is.True);
        }

        [Test]
        public void IsProbeRequest_IgnoresBenignTraffic()
        {
            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/api/users", "GET")),
                Is.False);

            Assert.That(
                AttackWaveProbe.IsProbeRequest(BuildContext("::1", "/static/js/app.js", "GET")),
                Is.False);
        }

        private static Context BuildContext(string ip, string path, string method, IDictionary<string, string> query = null)
        {
            return new Context
            {
                RemoteAddress = ip,
                Route = path,
                Url = path,
                FullUrl = path,
                Method = method,
                Query = query ?? new Dictionary<string, string>(),
                Headers = new Dictionary<string, string>(),
                Cookies = new Dictionary<string, string>(),
                Source = "test",
            };
        }
    }
}
