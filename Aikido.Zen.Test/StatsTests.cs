
using Aikido.Zen.Core.Models;


namespace Aikido.Zen.Test
{
    [TestFixture]
    public class StatsTests
    {
        private const int DefaultMaxPerfSamples = 50;
        private const int DefaultMaxCompressedStats = 5;
        private const string TestOperation = "test_operation";
        private const string TestOperationKind = "test_kind";
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
            stats.OnInspectedCall(TestOperation, TestOperationKind, 10.0, false, false, false);
            stats.InterceptorThrewError(TestOperation, TestOperationKind);

            // Allow some time to pass to ensure StartedAt changes significantly
            Thread.Sleep(10); // Sleep for 10ms

            stats.Reset();
            var newStartedAt = stats.StartedAt;

            Assert.That(stats.IsEmpty(), Is.True, "Stats should be empty after reset.");
            Assert.That(stats.Operations, Is.Empty, "Operations dictionary should be empty after reset.");
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
        public void InterceptorThrewError_IncrementsOperationTotalAndErrors()
        {
            var stats = new Stats();
            stats.InterceptorThrewError(TestOperation, TestOperationKind);

            Assert.That(stats.Operations.ContainsKey(TestOperation), Is.True);
            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.Total, Is.EqualTo(1));
            Assert.That(operationStats.InterceptorThrewError, Is.EqualTo(1));
            Assert.That(operationStats.WithoutContext, Is.EqualTo(0));
            Assert.That(operationStats.AttacksDetected.Total, Is.EqualTo(0));
            Assert.That(operationStats.Durations, Is.Empty);
            Assert.That(operationStats.CompressedTimings, Is.Empty);
            Assert.That(operationStats.Kind, Is.EqualTo(TestOperationKind));
            Assert.That(stats.IsEmpty(), Is.False);
        }

        [Test]
        public void OnInspectedCall_TracksTotalsAndDurations()
        {
            var stats = new Stats();

            // 1. Call without context
            stats.OnInspectedCall(TestOperation, TestOperationKind, 10.0, attackDetected: false, blocked: false, withoutContext: true);
            Assert.That(stats.Operations.ContainsKey(TestOperation), Is.True);
            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.Total, Is.EqualTo(1));
            Assert.That(operationStats.WithoutContext, Is.EqualTo(1));
            Assert.That(operationStats.Durations, Is.Empty, "Duration should not be recorded for withoutContext=true");
            Assert.That(operationStats.AttacksDetected.Total, Is.EqualTo(0));
            Assert.That(stats.HasCompressedStats(), Is.False);
            Assert.That(stats.IsEmpty(), Is.False);
            Assert.That(operationStats.Kind, Is.EqualTo(TestOperationKind));

            // 2. Call with context, no attack
            stats.OnInspectedCall(TestOperation, TestOperationKind, 20.5, attackDetected: false, blocked: false, withoutContext: false);
            Assert.That(operationStats.Total, Is.EqualTo(2));
            Assert.That(operationStats.WithoutContext, Is.EqualTo(1));
            Assert.That(operationStats.Durations, Has.Count.EqualTo(1));
            Assert.That(operationStats.Durations[0], Is.EqualTo(20.5).Within(Tolerance));
            Assert.That(operationStats.AttacksDetected.Total, Is.EqualTo(0));

            // 3. Call with context, attack detected, not blocked
            stats.OnInspectedCall(TestOperation, TestOperationKind, 30.0, attackDetected: true, blocked: false, withoutContext: false);
            Assert.That(operationStats.Total, Is.EqualTo(3));
            Assert.That(operationStats.WithoutContext, Is.EqualTo(1));
            Assert.That(operationStats.Durations, Has.Count.EqualTo(2));
            Assert.That(operationStats.Durations[1], Is.EqualTo(30.0).Within(Tolerance));
            Assert.That(operationStats.AttacksDetected.Total, Is.EqualTo(1));
            Assert.That(operationStats.AttacksDetected.Blocked, Is.EqualTo(0));

            // 4. Call with context, attack detected, blocked
            stats.OnInspectedCall(TestOperation, TestOperationKind, 40.0, attackDetected: true, blocked: true, withoutContext: false);
            Assert.That(operationStats.Total, Is.EqualTo(4));
            Assert.That(operationStats.WithoutContext, Is.EqualTo(1));
            Assert.That(operationStats.Durations, Has.Count.EqualTo(3));
            Assert.That(operationStats.Durations[2], Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(operationStats.AttacksDetected.Total, Is.EqualTo(2));
            Assert.That(operationStats.AttacksDetected.Blocked, Is.EqualTo(1));
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
                stats.OnInspectedCall(TestOperation, TestOperationKind, sequence[i], false, false, false);
                // Compression hasn't run yet, only adding
            }

            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.Durations, Has.Count.EqualTo(maxSamples), "Durations should be full before compression is triggered.");
            Assert.That(operationStats.CompressedTimings, Is.Empty);
            Assert.That(stats.HasCompressedStats(), Is.False);

            // This next call will trigger the compression because the count is already >= maxSamples
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            stats.OnInspectedCall(TestOperation, TestOperationKind, 999.0, false, false, false); // Add one more sample to trigger compression
            var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Now compression should have occurred, clearing the list before adding the 999.0
            Assert.That(operationStats.Durations, Has.Count.EqualTo(1), "Durations should contain only the sample added after compression");
            Assert.That(operationStats.Durations[0], Is.EqualTo(999.0).Within(Tolerance), "The post-compression sample should be present");
            Assert.That(operationStats.CompressedTimings, Has.Count.EqualTo(1), "Should have one compressed timing block");
            Assert.That(stats.HasCompressedStats(), Is.True);

            var compressed = operationStats.CompressedTimings[0];
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
                    stats.OnInspectedCall(TestOperation, TestOperationKind, (cycle * maxSamples) + i + 1.0, false, false, false);
                }
                // At the end of each inner loop, the Durations list is full.
                // Compression for cycle N happens on the first call of cycle N+1.
            }

            // After the loop (cycle 3 finishes), Durations contains 31-40.
            // CompressedTimings contains [Block0, Block1, Block2].
            // We need one more call to trigger compression of 31-40 and the removal.
            stats.OnInspectedCall(TestOperation, TestOperationKind, 999.0, false, false, false);

            // Now CompressedTimings should be [Block1, Block2, Block3]
            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.CompressedTimings, Has.Count.EqualTo(maxCompressed), "Should only keep maxCompressed blocks");

            // Optional: Check if the oldest block was removed (e.g., check CompressedAt or Average)
            // The first block would have average related to cycle 0 (1..10)
            // The kept blocks should be related to cycles 1, 2, 3
            double firstKeptAverage = operationStats.CompressedTimings[0].AverageInMS;
            double expectedFirstAverageCycle1 = Enumerable.Range(1 * maxSamples + 1, maxSamples).Average(x => (double)x); // Avg of 11..20
            Assert.That(firstKeptAverage, Is.EqualTo(expectedFirstAverageCycle1).Within(Tolerance), "Oldest block should have been removed");
        }

        [Test]
        public void ForceCompress_CompressesExistingDurations()
        {
            var stats = new Stats(DefaultMaxPerfSamples, DefaultMaxCompressedStats);
            stats.OnInspectedCall(TestOperation, TestOperationKind, 10.0, false, false, false);
            stats.OnInspectedCall(TestOperation, TestOperationKind, 20.0, false, false, false);

            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.Durations, Has.Count.EqualTo(2));
            Assert.That(operationStats.CompressedTimings, Is.Empty);
            Assert.That(stats.HasCompressedStats(), Is.False);

            stats.ForceCompress();

            Assert.That(operationStats.Durations, Is.Empty, "Durations should be cleared after ForceCompress");
            Assert.That(operationStats.CompressedTimings, Has.Count.EqualTo(1), "Should have one compressed block after ForceCompress");
            Assert.That(stats.HasCompressedStats(), Is.True);
            Assert.That(operationStats.CompressedTimings[0].AverageInMS, Is.EqualTo(15.0).Within(Tolerance)); // Avg of 10, 20
        }

        [Test]
        public void ForceCompress_DoesNothing_WhenNoDurations()
        {
            var stats = new Stats();
            // stats.EnsureOperationStats(TestOperation, TestOperationKind); // Ensure operation exists but no durations -- This is internal now
            // Accessing stats.Operations doesn't create the operation automatically
            // Let's explicitly add it if needed, though ForceCompress handles non-existent operations gracefully.

            Assert.That(stats.Operations.ContainsKey(TestOperation), Is.False); // Operation shouldn't exist yet

            stats.ForceCompress(); // Should do nothing as there are no operations

            Assert.That(stats.Operations.ContainsKey(TestOperation), Is.False);
            Assert.That(stats.HasCompressedStats(), Is.False);

            // Now test with an existing operation but no durations
            stats.InterceptorThrewError(TestOperation, TestOperationKind); // Creates operation with no durations
            stats.Operations[TestOperation].InterceptorThrewError = 0; // Reset the error count just added
            stats.Operations[TestOperation].Total = 0; // Reset total count

            var operationStats = stats.Operations[TestOperation];
            Assert.That(operationStats.Durations, Is.Empty);
            Assert.That(operationStats.CompressedTimings, Is.Empty);

            stats.ForceCompress(); // Should do nothing for this operation

            Assert.That(operationStats.Durations, Is.Empty);
            Assert.That(operationStats.CompressedTimings, Is.Empty);
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

        /// <summary>
        /// Tests that the Stats class handles concurrent operations correctly,
        /// ensuring that counters are updated atomically and consistently across threads.
        /// </summary>
        [Test]
        public async Task ConcurrentOperations_ProduceConsistentResults()
        {
            // Arrange
            var stats = new Stats(maxPerfSamplesInMem: 50, maxCompressedStatsInMem: 5); // Use reasonably small limits for perf samples
            int numThreads = 10;
            int opsPerThread = 1000; // Increase ops for better chance of races if they exist

            // Expected counts - must be updated atomically (using long for safety with Interlocked)
            long expectedRequests = 0;
            long expectedAborted = 0;
            long expectedGlobalAttacks = 0;
            long expectedGlobalBlocked = 0;
            long expectedOp1Total = 0;
            long expectedOp1ContextCalls = 0; // calls *with* context that recorded duration
            long expectedOp1NoContext = 0;
            long expectedOp1Attacks = 0;
            long expectedOp1Blocked = 0;
            long expectedOp2Total = 0;
            long expectedOp2Errors = 0;

            var tasks = new List<Task>();
            // Use ThreadLocal for thread-safe random number generation
            using var threadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    Random random = threadLocalRandom.Value; // Get thread-specific Random instance
                    for (int j = 0; j < opsPerThread; j++)
                    {
                        int choice = random.Next(5); // Randomly choose an operation

                        switch (choice)
                        {
                            case 0: // OnRequest
                                stats.OnRequest();
                                Interlocked.Increment(ref expectedRequests);
                                break;
                            case 1: // OnAbortedRequest
                                stats.OnAbortedRequest();
                                Interlocked.Increment(ref expectedAborted);
                                // Aborted requests do not count towards Requests.Total based on existing tests
                                break;
                            case 2: // OnDetectedAttack (Global)
                                bool blocked = random.Next(2) == 0;
                                stats.OnDetectedAttack(blocked);
                                Interlocked.Increment(ref expectedGlobalAttacks);
                                if (blocked) Interlocked.Increment(ref expectedGlobalBlocked);
                                break;
                            case 3: // OnInspectedCall for "Op1"
                                const string opName = "Op1";
                                const string opKind = "KindA";
                                bool op1WithoutContext = random.Next(4) == 0; // 25% chance no context
                                bool op1Attack = !op1WithoutContext && random.Next(5) == 0; // 20% chance attack (if context)
                                bool op1Blocked = op1Attack && random.Next(2) == 0; // 50% chance blocked (if attack)
                                double duration = op1WithoutContext ? 0 : random.NextDouble() * 100;

                                stats.OnInspectedCall(opName, opKind, duration, op1Attack, op1Blocked, op1WithoutContext);

                                Interlocked.Increment(ref expectedOp1Total);
                                if (op1WithoutContext)
                                {
                                    Interlocked.Increment(ref expectedOp1NoContext);
                                }
                                else
                                {
                                    Interlocked.Increment(ref expectedOp1ContextCalls);
                                    if (op1Attack) Interlocked.Increment(ref expectedOp1Attacks);
                                    if (op1Blocked) Interlocked.Increment(ref expectedOp1Blocked);
                                }
                                break;
                            case 4: // InterceptorThrewError for "Op2"
                                const string opName2 = "Op2";
                                const string opKind2 = "KindB";
                                stats.InterceptorThrewError(opName2, opKind2);
                                Interlocked.Increment(ref expectedOp2Total);
                                Interlocked.Increment(ref expectedOp2Errors);
                                break;
                        }
                        // Small sleep can sometimes help expose race conditions, but makes test slower.
                        // Thread.Sleep(random.Next(0, 2));
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            // Note: Use Assert.Multiple for clearer failure reporting
            Assert.Multiple(() =>
            {
                Assert.That(stats.Requests.Total, Is.EqualTo(expectedRequests), "Total Requests mismatch");
                Assert.That(stats.Requests.Aborted, Is.EqualTo(expectedAborted), "Aborted Requests mismatch");
                Assert.That(stats.Requests.AttacksDetected.Total, Is.EqualTo(expectedGlobalAttacks), "Global Attacks Total mismatch");
                Assert.That(stats.Requests.AttacksDetected.Blocked, Is.EqualTo(expectedGlobalBlocked), "Global Attacks Blocked mismatch");

                // Check Op1 stats
                Assert.That(stats.Operations.ContainsKey("Op1"), Is.True, "Op1 stats should exist after concurrent calls");
                if (stats.Operations.TryGetValue("Op1", out var op1Stats))
                {
                    Assert.That(op1Stats.Total, Is.EqualTo(expectedOp1Total), "Op1 Total mismatch");
                    Assert.That(op1Stats.WithoutContext, Is.EqualTo(expectedOp1NoContext), "Op1 WithoutContext mismatch");
                    Assert.That(op1Stats.AttacksDetected.Total, Is.EqualTo(expectedOp1Attacks), "Op1 Attacks Total mismatch");
                    Assert.That(op1Stats.AttacksDetected.Blocked, Is.EqualTo(expectedOp1Blocked), "Op1 Attacks Blocked mismatch");
                    // We cannot reliably assert the count of items in Durations or CompressedTimings
                    // due to the non-deterministic nature of when compression occurs across threads.
                    Assert.That(op1Stats.Kind, Is.EqualTo("KindA"), "Op1 Kind mismatch");
                }

                // Check Op2 stats
                Assert.That(stats.Operations.ContainsKey("Op2"), Is.True, "Op2 stats should exist after concurrent calls");
                if (stats.Operations.TryGetValue("Op2", out var op2Stats))
                {
                    Assert.That(op2Stats.Total, Is.EqualTo(expectedOp2Total), "Op2 Total mismatch");
                    Assert.That(op2Stats.InterceptorThrewError, Is.EqualTo(expectedOp2Errors), "Op2 Errors mismatch");
                    Assert.That(op2Stats.Kind, Is.EqualTo("KindB"), "Op2 Kind mismatch");
                }

                // Optional: Force compression and check results if needed, but adds complexity.
                // stats.ForceCompress();
                // // Add assertions based on compressed data if required
            });
        }
    }
}
