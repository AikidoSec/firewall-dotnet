using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test
{
    public class StatsTests
    {
        private Stats _stats;
        private const int DefaultMaxPerfSamples = 1000;
        private const int DefaultMaxCompressedStats = 100;

        [SetUp]
        public void Setup()
        {
            _stats = new Stats(DefaultMaxPerfSamples, DefaultMaxCompressedStats);
        }

        [Test]
        public void Constructor_CreatesValidInstance()
        {
            // Assert
            Assert.That(_stats, Is.Not.Null);
            Assert.That(_stats.Sinks, Is.Empty);
            Assert.That(_stats.Requests, Is.Not.Null);
            Assert.That(_stats.StartedAt, Is.GreaterThan(0));
        }

        [Test]
        public void Constructor_WithCustomValues_SetsCorrectLimits()
        {
            // Arrange & Act
            var customStats = new Stats(500, 50);

            // Assert
            Assert.That(customStats, Is.Not.Null);
            Assert.That(customStats.Sinks, Is.Empty);
        }

        [Test]
        public void Reset_ClearsAllData()
        {
            // Arrange
            _stats.InterceptorThrewError("testSink");
            _stats.OnDetectedAttack(true);

            // Act
            _stats.Reset();

            // Assert
            Assert.That(_stats.IsEmpty(), Is.True);
            Assert.That(_stats.Sinks, Is.Empty);
            Assert.That(_stats.Requests.Total, Is.EqualTo(0));
            Assert.That(_stats.Requests.AttacksDetected.Total, Is.EqualTo(0));
        }

        [Test]
        public void InterceptorThrewError_IncrementsCounts()
        {
            // Arrange
            const string sink = "testSink";

            // Act
            _stats.InterceptorThrewError(sink);

            // Assert
            Assert.That(_stats.Sinks[sink].Total, Is.EqualTo(1));
            Assert.That(_stats.Sinks[sink].InterceptorThrewError, Is.EqualTo(1));
        }

        [Test]
        public void OnDetectedAttack_Blocked_IncrementsBlockedAndTotal()
        {
            // Act
            _stats.OnDetectedAttack(true);

            // Assert
            Assert.That(_stats.Requests.AttacksDetected.Total, Is.EqualTo(1));
            Assert.That(_stats.Requests.AttacksDetected.Blocked, Is.EqualTo(1));
        }

        [Test]
        public void OnDetectedAttack_NotBlocked_OnlyIncrementsTotal()
        {
            // Act
            _stats.OnDetectedAttack(false);

            // Assert
            Assert.That(_stats.Requests.AttacksDetected.Total, Is.EqualTo(1));
            Assert.That(_stats.Requests.AttacksDetected.Blocked, Is.EqualTo(0));
        }

        [Test]
        public void HasCompressedStats_WithNoStats_ReturnsFalse()
        {
            // Assert
            Assert.That(_stats.HasCompressedStats(), Is.False);
        }

        [Test]
        public void CompressPerfSamples_CalculatesCorrectPercentiles()
        {
            // Arrange
            const string sink = "testSink";
            var durations = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
            _stats.Sinks[sink] = new MonitoredSinkStats { Durations = durations.ToList() };

            // Act
            _stats.ForceCompress();

            // Assert
            var compressedStats = _stats.Sinks[sink].CompressedTimings.First();
            Assert.That(compressedStats.AverageInMS, Is.EqualTo(5.5));
            Assert.That(compressedStats.Percentiles["50"], Is.EqualTo(5.5));
            Assert.That(compressedStats.Percentiles["75"], Is.EqualTo(7.75));
            Assert.That(compressedStats.Percentiles["90"], Is.EqualTo(9.1));
            Assert.That(compressedStats.Percentiles["95"], Is.EqualTo(9.55));
            Assert.That(compressedStats.Percentiles["99"], Is.EqualTo(9.91));
        }

        [Test]
        public void CompressPerfSamples_ClearsOriginalDurations()
        {
            // Arrange
            const string sink = "testSink";
            var durations = new[] { 1.0, 2.0, 3.0 };
            _stats.Sinks[sink] = new MonitoredSinkStats { Durations = durations.ToList() };

            // Act
            _stats.ForceCompress();

            // Assert
            Assert.That(_stats.Sinks[sink].Durations, Is.Empty);
            Assert.That(_stats.Sinks[sink].CompressedTimings, Has.Count.EqualTo(1));
        }

        [Test]
        public void CompressPerfSamples_RespectsMaxCompressedStats()
        {
            // Arrange
            const string sink = "testSink";
            _stats = new Stats(maxPerfSamplesInMem: 10, maxCompressedStatsInMem: 2);
            _stats.Sinks[sink] = new MonitoredSinkStats();

            // Act - Add 3 sets of stats
            for (int i = 0; i < 3; i++)
            {
                _stats.Sinks[sink].Durations = new[] { 1.0, 2.0, 3.0 }.ToList();
                _stats.ForceCompress();
            }

            // Assert
            Assert.That(_stats.Sinks[sink].CompressedTimings, Has.Count.EqualTo(2));
        }

        [Test]
        public void IsEmpty_WithNoData_ReturnsTrue()
        {
            // Assert
            Assert.That(_stats.IsEmpty(), Is.True);
        }

        [Test]
        public void IsEmpty_WithData_ReturnsFalse()
        {
            // Arrange
            _stats.OnDetectedAttack(true);

            // Assert
            Assert.That(_stats.IsEmpty(), Is.False);
        }

        [Test]
        public void CompressedAt_UsesCorrectTimestamp()
        {
            // Arrange
            const string sink = "testSink";
            var beforeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _stats.Sinks[sink] = new MonitoredSinkStats { Durations = new[] { 1.0, 2.0, 3.0 }.ToList() };

            // Act
            _stats.ForceCompress();
            var afterTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Assert
            var compressedAt = _stats.Sinks[sink].CompressedTimings.First().CompressedAt;
            Assert.That(compressedAt, Is.GreaterThanOrEqualTo(beforeTime));
            Assert.That(compressedAt, Is.LessThanOrEqualTo(afterTime));
        }

        [Test]
        public void ForceCompress_WithEmptySink_DoesNotCreateCompressedStats()
        {
            // Arrange
            const string sink = "mongodb";
            _stats.Sinks[sink] = new MonitoredSinkStats();

            // Act
            _stats.ForceCompress();

            // Assert
            Assert.That(_stats.HasCompressedStats(), Is.False);
            Assert.That(_stats.Sinks[sink].CompressedTimings, Is.Empty);
        }
    }
}
