using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;

// Make internals visible to the test project
[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Collects and manages inspection statistics for operations and requests.
    /// </summary>
    public class Stats
    {
        private readonly int _maxPerfSamplesInMem;
        private readonly int _maxCompressedStatsInMem;
        private Dictionary<string, OperationStats> _operations = new Dictionary<string, OperationStats>();
        private Requests _requests = new Requests();

        /// <summary>
        /// Initializes a new instance of the <see cref="Stats"/> class.
        /// </summary>
        /// <param name="maxPerfSamplesInMem">The maximum number of performance samples to keep in memory per operation before compressing.</param>
        /// <param name="maxCompressedStatsInMem">The maximum number of compressed statistic blocks to keep in memory per operation.</param>
        public Stats(int maxPerfSamplesInMem = 1000, int maxCompressedStatsInMem = 100)
        {
            _maxPerfSamplesInMem = maxPerfSamplesInMem;
            _maxCompressedStatsInMem = maxCompressedStatsInMem;
            Reset();
        }

        /// <summary>
        /// CopyOperations the statistics for each monitored operation.
        /// </summary>
        /// <param name="operations"></param>
        public void CopyOperations(IReadOnlyDictionary<string, OperationStats> operations)
        {
            foreach (var operation in operations)
            {
                var ov = operation.Value;
                // deep copy the operation stats
                _operations[operation.Key] = new OperationStats
                {
                    AttacksDetected = ov.AttacksDetected,
                    CompressedTimings = ov.CompressedTimings.Select(x => new CompressedTiming
                    {
                        AverageInMS = x.AverageInMS,
                        Percentiles = x.Percentiles,
                        CompressedAt = x.CompressedAt
                    }).ToList(),
                    Durations = ov.Durations.Count > 0 ? new List<double>(ov.Durations) : new List<double>(),
                    InterceptorThrewError = ov.InterceptorThrewError,
                    Total = ov.Total,
                    WithoutContext = ov.WithoutContext,
                    Operation = operation.Key,
                    Kind = ov.Kind,
                };
            }
        }

        /// <summary>
        /// Gets the statistics collected for each monitored operation.
        /// </summary>
        public IReadOnlyDictionary<string, OperationStats> Operations => _operations;

        /// <summary>
        /// Gets the Unix timestamp (in milliseconds) when the statistics collection started or was last reset.
        /// </summary>
        public long StartedAt { get; set; }

        /// <summary>
        /// Gets the Unix timestamp (in milliseconds) when the statistics collection ended.
        /// </summary>
        public long EndedAt { get; set; }

        /// <summary>
        /// Gets the statistics related to incoming requests.
        /// </summary>
        public Requests Requests => _requests;

        /// <summary>
        /// Resets all collected statistics and updates the start time.
        /// </summary>
        public void Reset()
        {
            _operations = new Dictionary<string, OperationStats>();
            _requests = new Requests
            {
                Total = 0,
                Aborted = 0,
                AttacksDetected = new AttacksDetected
                {
                    Total = 0,
                    Blocked = 0
                }
            };
            StartedAt = DateTimeHelper.UTCNowUnixMilliseconds();
        }

        /// <summary>
        /// Checks if any operation has compressed timing statistics.
        /// </summary>
        /// <returns>True if compressed statistics exist, false otherwise.</returns>
        public bool HasCompressedStats()
        {
            // Ensure CompressedTimings is not null before checking Any()
            return _operations.Any(op => op.Value.CompressedTimings != null && op.Value.CompressedTimings.Any());
        }

        /// <summary>
        /// Records that an interceptor threw an error for a specific operation.
        /// </summary>
        /// <param name="operation">The identifier of the operation.</param>
        /// <param name="kind">The kind of the operation.</param>
        public void InterceptorThrewError(string operation, string kind)
        {
            EnsureOperationStats(operation, kind);
            _operations[operation].Total++;
            _operations[operation].InterceptorThrewError++;
        }

        /// <summary>
        /// Records a detected attack during request processing.
        /// </summary>
        /// <param name="blocked">Indicates whether the detected attack was blocked.</param>
        public void OnDetectedAttack(bool blocked)
        {
            _requests.AttacksDetected.Total++;
            if (blocked)
            {
                _requests.AttacksDetected.Blocked++;
            }
        }

        /// <summary>
        /// Records the occurrence of a new request.
        /// </summary>
        public void OnRequest()
        {
            _requests.Total++;
        }

        /// <summary>
        /// Records that a request was aborted.
        /// </summary>
        public void OnAbortedRequest()
        {
            _requests.Aborted++;
        }

        /// <summary>
        /// Records the details of an inspected operation call.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <param name="kind">The kind of operation.</param>
        /// <param name="durationInMs">The duration of the call in milliseconds.</param>
        /// <param name="attackDetected">Indicates whether an attack was detected during this call.</param>
        /// <param name="blocked">Indicates whether the call was blocked due to a detected attack.</param>
        /// <param name="withoutContext">Indicates whether the call was inspected without context.</param>
        public void OnInspectedCall(string operation, string kind, double durationInMs, bool attackDetected, bool blocked, bool withoutContext)
        {
            EnsureOperationStats(operation, kind);
            var operationStats = _operations[operation];
            operationStats.Total++;

            if (withoutContext)
            {
                operationStats.WithoutContext++;
                return; // Do not record duration or attack details if without context
            }

            // Check if duration list needs compression before adding the new duration
            if (operationStats.Durations.Count >= _maxPerfSamplesInMem)
            {
                CompressPerfSamples(operation);
            }

            operationStats.Durations.Add(durationInMs);

            if (attackDetected)
            {
                operationStats.AttacksDetected.Total++;
                if (blocked)
                {
                    operationStats.AttacksDetected.Blocked++;
                }
            }
        }

        /// <summary>
        /// Forces the compression of performance samples for all operations that have accumulated samples.
        /// </summary>
        public void ForceCompress()
        {
            // Use ToList() to avoid modification during iteration if CompressPerfSamples modifies _operations
            foreach (var operation in _operations.Keys.ToList())
            {
                // Only compress if there are durations to compress
                if (_operations.TryGetValue(operation, out var operationStats) && operationStats.Durations.Any())
                {
                    CompressPerfSamples(operation);
                }
            }
        }

        /// <summary>
        /// Ensures that a statistics entry exists for the specified operation.
        /// </summary>
        /// <param name="operation">The identifier of the operation.</param>
        /// <param name="kind">The kind of the operation.</param>
        internal void EnsureOperationStats(string operation, string kind)
        {
            if (!_operations.ContainsKey(operation))
            {
                // Initialize with default values, including empty lists/dictionaries
                _operations[operation] = new OperationStats
                {
                    AttacksDetected = new AttacksDetected(),
                    CompressedTimings = new List<CompressedTiming>(),
                    Durations = new List<double>(),
                    Kind = kind,
                    Operation = operation
                };
            }
        }

        /// <summary>
        /// Compresses the collected performance duration samples for a specific operation into a single entry.
        /// </summary>
        /// <param name="operation">The identifier of the operation.</param>
        private void CompressPerfSamples(string operation)
        {
            if (!_operations.TryGetValue(operation, out var operationStats) || !operationStats.Durations.Any())
            {
                return; // Nothing to compress
            }

            var timings = operationStats.Durations;
            var averageInMs = timings.Average();

            var percentilesToCalculate = new List<int> { 50, 75, 90, 95, 99 };
            var calculatedPercentiles = CalculatePercentiles(percentilesToCalculate, timings);

            var compressedTiming = new CompressedTiming
            {
                AverageInMS = averageInMs,
                Percentiles = CreatePercentilesDictionary(percentilesToCalculate, calculatedPercentiles),
                CompressedAt = DateTimeHelper.UTCNowUnixMilliseconds()
            };

            // Add the new compressed data
            operationStats.CompressedTimings.Add(compressedTiming);

            // Ensure the compressed timings list does not exceed the maximum size
            if (operationStats.CompressedTimings.Count() > _maxCompressedStatsInMem)
            {
                operationStats.CompressedTimings.RemoveAt(0); // Remove the oldest entry
            }

            // Clear the durations list now that it's compressed
            operationStats.Durations.Clear();
        }

        /// <summary>
        /// Creates a dictionary mapping percentile keys (as strings) to their calculated values.
        /// </summary>
        /// <param name="percentileKeys">The list of percentile keys (e.g., 50, 75).</param>
        /// <param name="percentileValues">The corresponding list of calculated percentile values.</param>
        /// <returns>A dictionary with string keys (percentile numbers) and double values (calculated percentiles).</returns>
        private static Dictionary<string, double> CreatePercentilesDictionary(List<int> percentileKeys, List<double> percentileValues)
        {
            // Zip the two lists together, pairing each percentile key with its calculated value.
            return percentileKeys.Zip(percentileValues, (key, value) => new { Key = key.ToString(), Value = value })
                               // Convert the resulting sequence of key-value pairs into a dictionary.
                               .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// Calculates specific percentiles for a given list of numbers.
        /// </summary>
        /// <param name="percentiles">A list of percentiles to calculate (0-100).</param>
        /// <param name="values">The list of numbers to calculate percentiles from.</param>
        /// <returns>A list of calculated percentile values corresponding to the requested percentiles.</returns>
        /// <exception cref="ArgumentException">Thrown if the list is empty or if any percentile is outside the 0-100 range.</exception>
        internal static List<double> CalculatePercentiles(IList<int> percentiles, IList<double> values)
        {
            if (!values.Any())
            {
                throw new ArgumentException("Input list cannot be empty.", nameof(values));
            }

            if (percentiles.Any(p => p < 0 || p > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(percentiles), "Percentiles must be between 0 and 100.");
            }

            // Sort the list once upfront
            var sortedValues = values.OrderBy(v => v).ToList();

            // Map each requested percentile to its calculated value using the helper method
            return percentiles.Select(p => GetPercentileValue(p, sortedValues)).ToList();
        }

        /// <summary>
        /// Gets the value at a specific percentile within a sorted list.
        /// </summary>
        /// <param name="percentile">The percentile to find (0-100).</param>
        /// <param name="sortedValues">The pre-sorted list of values.</param>
        /// <returns>The value at the specified percentile.</returns>
        internal static double GetPercentileValue(int percentile, List<double> sortedValues)
        {
            if (percentile == 0)
            {
                // For 0th percentile, return the first element
                return sortedValues[0];
            }

            var count = sortedValues.Count;
            // cast to float to avoid integer division
            var kIndex = (int)Math.Ceiling(count * (percentile / 100f)) - 1;
            return sortedValues[kIndex];
        }

        /// <summary>
        /// Checks if there are no statistics collected (no operations, no requests).
        /// </summary>
        /// <returns>True if no statistics have been collected, false otherwise.</returns>
        public bool IsEmpty()
        {
            return !_operations.Any() &&
                   _requests.Total == 0 &&
                   _requests.AttacksDetected.Total == 0;
        }
    }
}
