using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // For Task.Delay
using Aikido.Zen.Core.Models;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class StatsTests
    {
        private const int DefaultMaxPerfSamples = 50;
        private const int DefaultMaxCompressedStats = 5;
        private const string TestSink = "test_sink";
        private const double Tolerance = 0.0001; // Tolerance for double comparisons

        // Helper to generate a sequence of doubles 1.0, 2.0, ... length.0
        private static List<double> GenerateSequence(int length) =>
            Enumerable.Range(1, length).Select(i => (double)i).ToList();

        // Helper equivalent to Node's shuffleArray (simple Fisher-Yates shuffle)
        private static List<double> Shuffle(List<double> list)
        {
            var rng = new Random();
            var n = list.Count;
            var shuffledList = new List<double>(list); // Create a copy
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                (shuffledList[k], shuffledList[n]) = (shuffledList[n], shuffledList[k]);
            }
            return shuffledList;
        }

        [Test]
        public void IsEmpty_ShouldBeTrue_ForNewStats()
        {
            var stats = new Stats();
            Assert.That(stats.IsEmpty(), Is.True);
        }

        [Test]
        public void HasCompressedStats_ShouldBeFalse_ForNewStats()
        {
            var stats = new Stats();
            Assert.That(stats.HasCompressedStats(), Is.False);
        }


        [Test]
        public void Reset_ClearsStatsAndUpdatesStartedAt()
        {
            var stats = new Stats(DefaultMaxPerfSamples, DefaultMaxCompressedStats);
            var initialStartedAt = stats.StartedAt;

            stats.OnRequest();
            stats.OnInspectedCall(TestSink, 10.0, false, false, false);
            stats.InterceptorThrewError(TestSink);

            // Allow some time to pass to ensure StartedAt changes significantly
            Thread.Sleep(10); // Sleep for 10ms

            stats.Reset();
            var newStartedAt = stats.StartedAt;

            Assert.That(stats.IsEmpty(), Is.True, "Stats should be empty after reset.");
            Assert.That(stats.Sinks, Is.Empty, "Sinks dictionary should be empty after reset.");
            Assert.That(stats.Requests.Total, Is.EqualTo(0), "Requests total should be 0 after reset.");
            Assert.That(stats.Requests.Aborted, Is.EqualTo(0), "Requests aborted should be 0 after reset.");
            Assert.That(stats.Requests.AttacksDetected.Total, Is.EqualTo(0), "Requests attacks total should be 0 after reset.");
            Assert.That(stats.Requests.AttacksDetected.Blocked, Is.EqualTo(0), "Requests attacks blocked should be 0 after reset.");
            Assert.That(newStartedAt, Is.GreaterThan(initialStartedAt), "StartedAt should be updated after reset.");
            Assert.That(stats.HasCompressedStats(), Is.False, "Should not have compressed stats after reset.");
        }

        [Test]
        public void OnRequest_IncrementsRequestTotal()
        {
            var stats = new Stats();
            stats.OnRequest();
            stats.OnRequest();
            Assert.That(stats.Requests.Total, Is.EqualTo(2));
            Assert.That(stats.IsEmpty(), Is.False);
        }

        [Test]
        public void OnAbortedRequest_IncrementsRequestAborted()
        {
            var stats = new Stats();
            stats.OnAbortedRequest();
            Assert.That(stats.Requests.Aborted, Is.EqualTo(1));
            // Aborted requests don't make stats non-empty by themselves if total is 0
            Assert.That(stats.IsEmpty(), Is.True);
            stats.OnRequest(); // Make non-empty
            Assert.That(stats.IsEmpty(), Is.False);

        }

        [Test]
        public void OnDetectedAttack_IncrementsRequestAttacks()
        {
            var stats = new Stats();

            stats.OnDetectedAttack(blocked: false);
            Assert.That(stats.Requests.AttacksDetected.Total, Is.EqualTo(1));
            Assert.That(stats.Requests.AttacksDetected.Blocked, Is.EqualTo(0));
            Assert.That(stats.IsEmpty(), Is.False); // Detecting attack makes it non-empty

            stats.OnDetectedAttack(blocked: true);
            Assert.That(stats.Requests.AttacksDetected.Total, Is.EqualTo(2));
            Assert.That(stats.Requests.AttacksDetected.Blocked, Is.EqualTo(1));
        }

        [Test]
        public void InterceptorThrewError_IncrementsSinkTotalAndErrors()
        {
            var stats = new Stats();
            stats.InterceptorThrewError(TestSink);

            Assert.That(stats.Sinks.ContainsKey(TestSink), Is.True);
            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.Total, Is.EqualTo(1));
            Assert.That(sinkStats.InterceptorThrewError, Is.EqualTo(1));
            Assert.That(sinkStats.WithoutContext, Is.EqualTo(0));
            Assert.That(sinkStats.AttacksDetected.Total, Is.EqualTo(0));
            Assert.That(sinkStats.Durations, Is.Empty);
            Assert.That(sinkStats.CompressedTimings, Is.Empty);
            Assert.That(stats.IsEmpty(), Is.False);
        }

        [Test]
        public void OnInspectedCall_TracksTotalsAndDurations()
        {
            var stats = new Stats();

            // 1. Call without context
            stats.OnInspectedCall(TestSink, 10.0, attackDetected: false, blocked: false, withoutContext: true);
            Assert.That(stats.Sinks.ContainsKey(TestSink), Is.True);
            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.Total, Is.EqualTo(1));
            Assert.That(sinkStats.WithoutContext, Is.EqualTo(1));
            Assert.That(sinkStats.Durations, Is.Empty, "Duration should not be recorded for withoutContext=true");
            Assert.That(sinkStats.AttacksDetected.Total, Is.EqualTo(0));
            Assert.That(stats.HasCompressedStats(), Is.False);
            Assert.That(stats.IsEmpty(), Is.False);

            // 2. Call with context, no attack
            stats.OnInspectedCall(TestSink, 20.5, attackDetected: false, blocked: false, withoutContext: false);
            Assert.That(sinkStats.Total, Is.EqualTo(2));
            Assert.That(sinkStats.WithoutContext, Is.EqualTo(1));
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(1));
            Assert.That(sinkStats.Durations[0], Is.EqualTo(20.5).Within(Tolerance));
            Assert.That(sinkStats.AttacksDetected.Total, Is.EqualTo(0));

            // 3. Call with context, attack detected, not blocked
            stats.OnInspectedCall(TestSink, 30.0, attackDetected: true, blocked: false, withoutContext: false);
            Assert.That(sinkStats.Total, Is.EqualTo(3));
            Assert.That(sinkStats.WithoutContext, Is.EqualTo(1));
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(2));
            Assert.That(sinkStats.Durations[1], Is.EqualTo(30.0).Within(Tolerance));
            Assert.That(sinkStats.AttacksDetected.Total, Is.EqualTo(1));
            Assert.That(sinkStats.AttacksDetected.Blocked, Is.EqualTo(0));

            // 4. Call with context, attack detected, blocked
            stats.OnInspectedCall(TestSink, 40.0, attackDetected: true, blocked: true, withoutContext: false);
            Assert.That(sinkStats.Total, Is.EqualTo(4));
            Assert.That(sinkStats.WithoutContext, Is.EqualTo(1));
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(3));
            Assert.That(sinkStats.Durations[2], Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(sinkStats.AttacksDetected.Total, Is.EqualTo(2));
            Assert.That(sinkStats.AttacksDetected.Blocked, Is.EqualTo(1));
        }


        [Test]
        public void OnInspectedCall_TriggersCompressionWhenMaxSamplesReached()
        {
            int maxSamples = 10;
            var stats = new Stats(maxPerfSamplesInMem: maxSamples, maxCompressedStatsInMem: DefaultMaxCompressedStats);
            var sequence = GenerateSequence(maxSamples); // 1.0 to 10.0

            // Add maxSamples durations (1 to 10)
            for (int i = 0; i < maxSamples; i++)
            {
                stats.OnInspectedCall(TestSink, sequence[i], false, false, false);
                // Compression hasn't run yet, only adding
            }

            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(maxSamples), "Durations should be full before compression is triggered.");
            Assert.That(sinkStats.CompressedTimings, Is.Empty);
            Assert.That(stats.HasCompressedStats(), Is.False);

            // This next call will trigger the compression because the count is already >= maxSamples
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            stats.OnInspectedCall(TestSink, 999.0, false, false, false); // Add one more sample to trigger compression
            var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Now compression should have occurred, clearing the list before adding the 999.0
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(1), "Durations should contain only the sample added after compression");
            Assert.That(sinkStats.Durations[0], Is.EqualTo(999.0).Within(Tolerance), "The post-compression sample should be present");
            Assert.That(sinkStats.CompressedTimings, Has.Count.EqualTo(1), "Should have one compressed timing block");
            Assert.That(stats.HasCompressedStats(), Is.True);

            var compressed = sinkStats.CompressedTimings[0];
            // The compressed block should contain stats for the sequence 1..10
            var expectedAverage = sequence.Average(); // Average of 1 to 10 is 5.5
            Assert.That(compressed.AverageInMS, Is.EqualTo(expectedAverage).Within(Tolerance));
            Assert.That(compressed.CompressedAt, Is.GreaterThanOrEqualTo(startTime).And.LessThanOrEqualTo(endTime));

            Assert.That(compressed.Percentiles.ContainsKey("50"), Is.True);
            Assert.That(compressed.Percentiles["50"], Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(compressed.Percentiles.ContainsKey("75"), Is.True);
            Assert.That(compressed.Percentiles["75"], Is.EqualTo(8.0).Within(Tolerance));
            Assert.That(compressed.Percentiles.ContainsKey("90"), Is.True);
            Assert.That(compressed.Percentiles["90"], Is.EqualTo(9.0).Within(Tolerance));
            Assert.That(compressed.Percentiles.ContainsKey("95"), Is.True);
            Assert.That(compressed.Percentiles["95"], Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(compressed.Percentiles.ContainsKey("99"), Is.True);
            Assert.That(compressed.Percentiles["99"], Is.EqualTo(10.0).Within(Tolerance));
        }


        [Test]
        public void Compression_RespectsMaxCompressedStatsLimit()
        {
            int maxSamples = 10;
            int maxCompressed = 3;
            var stats = new Stats(maxPerfSamplesInMem: maxSamples, maxCompressedStatsInMem: maxCompressed);

            // Trigger compression maxCompressed + 1 times (4 times: cycles 0, 1, 2, 3)
            for (int cycle = 0; cycle < maxCompressed + 1; cycle++)
            {
                for (int i = 0; i < maxSamples; i++)
                {
                    // Use different values each cycle to ensure compressed blocks are distinct
                    stats.OnInspectedCall(TestSink, (cycle * maxSamples) + i + 1.0, false, false, false);
                }
                // At the end of each inner loop, the Durations list is full.
                // Compression for cycle N happens on the first call of cycle N+1.
            }

            // After the loop (cycle 3 finishes), Durations contains 31-40.
            // CompressedTimings contains [Block0, Block1, Block2].
            // We need one more call to trigger compression of 31-40 and the removal.
            stats.OnInspectedCall(TestSink, 999.0, false, false, false);

            // Now CompressedTimings should be [Block1, Block2, Block3]
            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.CompressedTimings, Has.Count.EqualTo(maxCompressed), "Should only keep maxCompressed blocks");

            // Optional: Check if the oldest block was removed (e.g., check CompressedAt or Average)
            // The first block would have average related to cycle 0 (1..10)
            // The kept blocks should be related to cycles 1, 2, 3
            double firstKeptAverage = sinkStats.CompressedTimings[0].AverageInMS;
            double expectedFirstAverageCycle1 = Enumerable.Range(1 * maxSamples + 1, maxSamples).Average(x => (double)x); // Avg of 11..20
            Assert.That(firstKeptAverage, Is.EqualTo(expectedFirstAverageCycle1).Within(Tolerance), "Oldest block should have been removed");
        }

        [Test]
        public void ForceCompress_CompressesExistingDurations()
        {
            var stats = new Stats(DefaultMaxPerfSamples, DefaultMaxCompressedStats);
            stats.OnInspectedCall(TestSink, 10.0, false, false, false);
            stats.OnInspectedCall(TestSink, 20.0, false, false, false);

            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.Durations, Has.Count.EqualTo(2));
            Assert.That(sinkStats.CompressedTimings, Is.Empty);
            Assert.That(stats.HasCompressedStats(), Is.False);

            stats.ForceCompress();

            Assert.That(sinkStats.Durations, Is.Empty, "Durations should be cleared after ForceCompress");
            Assert.That(sinkStats.CompressedTimings, Has.Count.EqualTo(1), "Should have one compressed block after ForceCompress");
            Assert.That(stats.HasCompressedStats(), Is.True);
            Assert.That(sinkStats.CompressedTimings[0].AverageInMS, Is.EqualTo(15.0).Within(Tolerance)); // Avg of 10, 20
        }

        [Test]
        public void ForceCompress_DoesNothing_WhenNoDurations()
        {
            var stats = new Stats();
            // stats.EnsureSinkStats(TestSink); // Ensure sink exists but no durations -- This is done implicitly by accessing Sinks
            // Accessing stats.Sinks doesn't create the sink automatically
            // Let's explicitly add it if needed, though ForceCompress handles non-existent sinks gracefully.

            Assert.That(stats.Sinks.ContainsKey(TestSink), Is.False); // Sink shouldn't exist yet
            // These properties don't exist on the Stats class itself
            // Assert.That(stats.Durations, Is.Empty);
            // Assert.That(stats.CompressedTimings, Is.Empty);

            stats.ForceCompress(); // Should do nothing as there are no sinks

            Assert.That(stats.Sinks.ContainsKey(TestSink), Is.False);
            Assert.That(stats.HasCompressedStats(), Is.False);

            // Now test with an existing sink but no durations
            stats.InterceptorThrewError(TestSink); // Creates sink with no durations
            stats.Sinks[TestSink].InterceptorThrewError = 0; // Reset the error count just added
            stats.Sinks[TestSink].Total = 0; // Reset total count

            var sinkStats = stats.Sinks[TestSink];
            Assert.That(sinkStats.Durations, Is.Empty);
            Assert.That(sinkStats.CompressedTimings, Is.Empty);

            stats.ForceCompress(); // Should do nothing for this sink

            Assert.That(sinkStats.Durations, Is.Empty);
            Assert.That(sinkStats.CompressedTimings, Is.Empty);
            Assert.That(stats.HasCompressedStats(), Is.False);
        }

        // --- Direct Percentile Calculation Tests ---
        // (These tests now directly call the internal Stats.CalculatePercentiles method)

        [Test]
        public void CalculatePercentiles_SimpleCases()
        {
            var list = GenerateSequence(100); // 1 to 100
            var shuffledList = Shuffle(list);

            // Mimic stubsSimple from Node test
            Assert.That(Stats.CalculatePercentiles(new List<int> { 0 }, shuffledList)[0], Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(Stats.CalculatePercentiles(new List<int> { 25 }, shuffledList)[0], Is.EqualTo(25.0).Within(Tolerance));
            Assert.That(Stats.CalculatePercentiles(new List<int> { 50 }, shuffledList)[0], Is.EqualTo(50.0).Within(Tolerance));
            Assert.That(Stats.CalculatePercentiles(new List<int> { 75 }, shuffledList)[0], Is.EqualTo(75.0).Within(Tolerance));
            Assert.That(Stats.CalculatePercentiles(new List<int> { 100 }, shuffledList)[0], Is.EqualTo(100.0).Within(Tolerance));

            // Mimic Node test: { percentile: 75, list: shuffleArray(generateArraySimple(100).concat(generateArraySimple(30))), result: 68 }
            var combinedList = GenerateSequence(100).Concat(GenerateSequence(30)).ToList();
            var shuffledCombinedList = Shuffle(combinedList); // List of 130 elements
                                                              // Expected: P75 = ceil(130 * 0.75) - 1 = ceil(97.5) - 1 = 98 - 1 = 97.
                                                              // Need to figure out the value at index 97 in the sorted combined list.
                                                              // Sorted list: [1..30 repeated, 31..100]. Index 97 corresponds to value 68 (30 + (97-29))? No.
                                                              // Sorted: [1..30, 1..100]. Total 130. Sorted unique: [1..100].
                                                              // Let's sort the actual combined list to find the value at index 97.
            combinedList.Sort();
            double expectedValueAtIndex97 = combinedList[97]; // Should be 68 based on Node test
            Assert.That(Stats.CalculatePercentiles(new List<int> { 75 }, shuffledCombinedList)[0], Is.EqualTo(expectedValueAtIndex97).Within(Tolerance), "P75 for combined list");
            // Let's double-check the value 68 logic. Sorted list has 1..30, then 1..100. So it's [1,1, 2,2, ..., 30,30, 31, 32, ..., 100]
            // Indices 0-59 are pairs 1-30. Indices 60-129 are 31-100.
            // Index 97 is within 60-129 range. Value = (97-60) + 31 = 37 + 31 = 68. Yes, logic is correct.
            Assert.That(expectedValueAtIndex97, Is.EqualTo(68.0).Within(Tolerance), "Confirming expected value at index 97 is 68");
        }

        [Test]
        public void CalculatePercentiles_NegativeValues()
        {
            var list1 = Shuffle(new List<double> { -1, -2, -3, -4, -5 });
            Assert.That(Stats.CalculatePercentiles(new List<int> { 50 }, list1)[0], Is.EqualTo(-3.0).Within(Tolerance));

            var list2 = Shuffle(new List<double> { 7, 6, -1, -2, -3, -4, -5 });
            Assert.That(Stats.CalculatePercentiles(new List<int> { 50 }, list2)[0], Is.EqualTo(-2.0).Within(Tolerance));
        }

        [Test]
        public void CalculatePercentiles_MultiplePercentiles()
        {
            var list = Shuffle(GenerateSequence(100)); // 1 to 100
            var percentilesToCalc = new List<int> { 0, 25, 50, 75, 100 };
            var results = Stats.CalculatePercentiles(percentilesToCalc, list);
            var expectedResults = new List<double> { 1.0, 25.0, 50.0, 75.0, 100.0 };

            Assert.That(results, Is.EqualTo(expectedResults).Within(Tolerance));
        }

        [Test]
        public void CalculatePercentiles_ThrowsOnEmptyList()
        {
            var emptyList = new List<double>();
            var percentilesToCalc = new List<int> { 50 };
            Assert.Throws<ArgumentException>(() => Stats.CalculatePercentiles(percentilesToCalc, emptyList));
        }

        [Test]
        public void CalculatePercentiles_ThrowsOnInvalidPercentileRange()
        {
            var validList = new List<double> { 1.0, 2.0, 3.0 };
            var percentilesLess = new List<int> { -1 };
            var percentilesMore = new List<int> { 101 };

            Assert.Throws<ArgumentOutOfRangeException>(() => Stats.CalculatePercentiles(percentilesLess, validList));
            Assert.Throws<ArgumentOutOfRangeException>(() => Stats.CalculatePercentiles(percentilesMore, validList));
        }
    }
}
