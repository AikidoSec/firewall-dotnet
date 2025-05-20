using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;

// Make internals visible to the test project
[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Collects and manages inspection statistics for operations and requests.
    /// </summary>
    public class AgentStats
    {
        private readonly int _maxPerfSamplesInMem;
        private readonly int _maxCompressedStatsInMem;
        private ConcurrentDictionary<string, OperationStats> _operations = new ConcurrentDictionary<string, OperationStats>();
        private Requests _requests = new Requests();
        private readonly ConcurrentLFUDictionary<string, Host> _hostnames;
        private readonly ConcurrentLFUDictionary<string, UserExtended> _users;
        private readonly ConcurrentLFUDictionary<string, Route> _routes;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentStats"/> class.
        /// </summary>
        /// <param name="maxPerfSamplesInMem">The maximum number of performance samples to keep in memory per operation before compressing.</param>
        /// <param name="maxCompressedStatsInMem">The maximum number of compressed statistic blocks to keep in memory per operation.</param>
        /// <param name="maxHostnames">The maximum number of hostnames to track.</param>
        /// <param name="maxUsers">The maximum number of users to track.</param>
        /// <param name="maxRoutes">The maximum number of routes to track.</param>
        public AgentStats(int maxPerfSamplesInMem = 1000, int maxCompressedStatsInMem = 100, int maxHostnames = 2000, int maxUsers = 2000, int maxRoutes = 5000)
        {
            _maxPerfSamplesInMem = maxPerfSamplesInMem;
            _maxCompressedStatsInMem = maxCompressedStatsInMem;
            _hostnames = new ConcurrentLFUDictionary<string, Host>(maxHostnames);
            _users = new ConcurrentLFUDictionary<string, UserExtended>(maxUsers);
            _routes = new ConcurrentLFUDictionary<string, Route>(maxRoutes);
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
        /// Gets the collection of tracked hostnames.
        /// </summary>
        public IEnumerable<Host> Hostnames => _hostnames.GetValues();

        /// <summary>
        /// Gets the collection of tracked users.
        /// </summary>
        public IEnumerable<UserExtended> Users => _users.GetValues();

        /// <summary>
        /// Gets the collection of tracked routes.
        /// </summary>
        public IEnumerable<Route> Routes => _routes.GetValues();

        /// <summary>
        /// Resets all collected statistics and updates the start time.
        /// </summary>
        public void Reset()
        {
            _operations = new ConcurrentDictionary<string, OperationStats>();
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
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
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
            var operationStats = _operations[operation];
            Interlocked.Increment(ref operationStats.Total);
            Interlocked.Increment(ref operationStats.InterceptorThrewError);
        }

        /// <summary>
        /// Records a detected attack during request processing.
        /// </summary>
        public void OnDetectedAttack(bool blocked = false)
        {
            Interlocked.Increment(ref _requests.AttacksDetected.Total);
            if (blocked)
            {
                Interlocked.Increment(ref _requests.AttacksDetected.Blocked);
            }
        }

        /// <summary>
        /// Records a blocked attack during request processing.
        /// </summary>
        internal void OnBlockedAttack()
        {
            Interlocked.Increment(ref _requests.AttacksDetected.Blocked);
        }

        /// <summary>
        /// Records the occurrence of a new request.
        /// </summary>
        public void OnRequest()
        {
            Interlocked.Increment(ref _requests.Total);
        }

        /// <summary>
        /// Records that a request was aborted.
        /// </summary>
        public void OnAbortedRequest()
        {
            Interlocked.Increment(ref _requests.Aborted);
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
            Interlocked.Increment(ref operationStats.Total);

            if (withoutContext)
            {
                Interlocked.Increment(ref operationStats.WithoutContext);
                return; // Do not record duration or attack details if without context
            }

            // Check if duration list needs compression before adding the new duration
            lock (operationStats)
            {
                if (operationStats.Durations.Count >= _maxPerfSamplesInMem)
                {
                    CompressPerfSamples(operation);
                }

                operationStats.Durations.Add(durationInMs);

                if (attackDetected)
                {
                    Interlocked.Increment(ref operationStats.AttacksDetected.Total);
                    if (blocked)
                    {
                        Interlocked.Increment(ref operationStats.AttacksDetected.Blocked);
                    }
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
            _operations.AddOrUpdate(operation,
                // Add function - creates new stats if key doesn't exist
                _ => new OperationStats
                {
                    AttacksDetected = new AttacksDetected(),
                    CompressedTimings = new List<CompressedTiming>(),
                    Durations = new List<double>(),
                    Kind = kind,
                    Operation = operation
                },
                // Update function - keeps existing stats if key exists
                (_, existingStats) => existingStats);
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

        /// <summary>
        /// Adds or updates a hostname in the statistics.
        /// </summary>
        /// <param name="hostname">The hostname to add or update.</param>
        public void AddHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return;
            var hostParts = hostname.Split(':');
            var name = hostParts[0];
            var port = hostParts.Length > 1 ? hostParts[1] : "80";
            int.TryParse(port, out int portNumber);

            var key = $"{name}:{port}";
            _hostnames.TryGet(key, out var host);
            if (host == null)
            {
                host = new Host { Hostname = name, Port = portNumber };
            }

            // when updating, we keep track of hits
            _hostnames.AddOrUpdate(key, host);
        }

        /// <summary>
        /// Adds or updates a user in the statistics.
        /// </summary>
        /// <param name="user">The user to add or update.</param>
        /// <param name="ipAddress">The IP address associated with this user access.</param>
        public void AddUser(User user, string ipAddress)
        {
            if (user == null || string.IsNullOrEmpty(user.Id))
                return;

            UserExtended userExtended;
            if (_users.TryGet(user.Id, out var existingUser))
            {
                // User exists, update details before calling AddOrUpdate
                existingUser.Name = user.Name; // Update name in case it changed
                existingUser.LastIpAddress = ipAddress;
                existingUser.LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
                userExtended = existingUser;
            }
            else
            {
                // User does not exist, create a new one
                userExtended = new UserExtended(user.Id, user.Name)
                {
                    LastIpAddress = ipAddress,
                    LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds()
                };
            }

            // AddOrUpdate handles incrementing hits and eviction
            _users.AddOrUpdate(user.Id, userExtended);
        }

        /// <summary>
        /// Adds or updates a route in the statistics.
        /// </summary>
        /// <param name="context">The context containing the route information.</param>
        public void AddRoute(Context context)
        {
            if (context == null || string.IsNullOrEmpty(context.Route)) return; // Check Route null/empty

            Route route;
            if (_routes.TryGet(context.Route, out var existingRoute))
            {
                // Route exists, update API info before calling AddOrUpdate
                OpenAPIHelper.UpdateApiInfo(context, existingRoute, EnvironmentHelper.MaxApiDiscoverySamples);
                route = existingRoute;
            }
            else
            {
                // Route does not exist, create a new one
                route = new Route
                {
                    Path = context.Route,
                    Method = context.Method,
                    ApiSpec = OpenAPIHelper.GetApiInfo(context),
                };
            }

            // AddOrUpdate handles incrementing hits and eviction
            _routes.AddOrUpdate(context.Route, route);
        }
    }
}
