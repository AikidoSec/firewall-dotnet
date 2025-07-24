using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Collects and manages AI operation statistics.
    /// </summary>
    public class AiStats
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<string, AiInfo> _aiProviders = new ConcurrentDictionary<string, AiInfo>();

        /// <summary>
        /// Gets the AI statistics for each provider.
        /// </summary>
        public IReadOnlyDictionary<string, AiInfo> Providers => _aiProviders;

        /// <summary>
        /// Creates deep copies of the AI statistics for each provider.
        /// </summary>
        /// <param name="providers">The collection to copy AI statistics to.</param>
        public void CopyProviders(ICollection<AiInfo> providers)
        {
            foreach (var provider in _aiProviders.Values)
            {
                // Deep copy the AiInfo
                var aiInfoCopy = new AiInfo
                {
                    Provider = provider.Provider,
                    Model = provider.Model,
                    Calls = provider.Calls,
                    Tokens = new AiTokens
                    {
                        Input = provider.Tokens.Input,
                        Output = provider.Tokens.Output
                    },
                    Routes = provider.Routes?.Select(route => new AiRoute
                    {
                        Path = route.Path,
                        Method = route.Method,
                        Requests = route.Requests,
                        Calls = route.Calls,
                        Tokens = new AiTokens
                        {
                            Input = route.Tokens.Input,
                            Output = route.Tokens.Output
                        }
                    }).ToList() ?? new List<AiRoute>()
                };
                providers.Add(aiInfoCopy);
            }
        }

        /// <summary>
        /// Resets all collected AI statistics.
        /// </summary>
        public void Reset()
        {
            _aiProviders = new ConcurrentDictionary<string, AiInfo>();
        }

        /// <summary>
        /// Records an AI call with token usage.
        /// </summary>
        /// <param name="provider">The AI provider name.</param>
        /// <param name="model">The AI model name.</param>
        /// <param name="inputTokens">Number of input tokens used.</param>
        /// <param name="outputTokens">Number of output tokens generated.</param>
        /// <param name="route">Optional route information.</param>
        public void OnAiCall(string provider, string model, long inputTokens = 0, long outputTokens = 0, string route = null)
        {
            if (string.IsNullOrEmpty(provider)) return;

            var key = $"{provider}:{model}";
            _aiProviders.AddOrUpdate(key,
                // Add function - creates new AiInfo if key doesn't exist
                _ => CreateNewAiInfo(provider, model, inputTokens, outputTokens, route),
                // Update function - updates existing AiInfo
                (_, existing) => UpdateExistingAiInfo(existing, inputTokens, outputTokens, route));
        }

        /// <summary>
        /// Adds or updates AI statistics for a specific provider, model, and route.
        /// </summary>
        /// <param name="provider">The AI provider name.</param>
        /// <param name="model">The AI model name.</param>
        /// <param name="route">The route object containing request information.</param>
        /// <param name="inputTokens">Number of input tokens used.</param>
        /// <param name="outputTokens">Number of output tokens generated.</param>
        public void AddAiStats(string provider, string model, Route route, long inputTokens, long outputTokens)
        {
            if (string.IsNullOrEmpty(provider)) return;

            var routePath = route?.Path;
            var key = $"{provider}:{model}";
            _aiProviders.AddOrUpdate(key,
                // Add function - creates new AiInfo if key doesn't exist
                _ => CreateNewAiInfo(provider, model, inputTokens, outputTokens, routePath),
                // Update function - updates existing AiInfo
                (_, existing) => UpdateExistingAiInfo(existing, inputTokens, outputTokens, routePath));
        }

        /// <summary>
        /// Creates a new AiInfo instance.
        /// </summary>
        private AiInfo CreateNewAiInfo(string provider, string model, long inputTokens, long outputTokens, string route)
        {
            var aiInfo = new AiInfo
            {
                Provider = provider,
                Model = model,
                Calls = 1,
                Tokens = new AiTokens
                {
                    Input = inputTokens,
                    Output = outputTokens,
                },
                Routes = new List<AiRoute>()
            };

            if (!string.IsNullOrEmpty(route))
            {
                var routes = (List<AiRoute>)aiInfo.Routes;
                routes.Add(new AiRoute
                {
                    Path = route,
                    Requests = 1,
                    Calls = 1,
                    Tokens = new AiTokens
                    {
                        Input = inputTokens,
                        Output = outputTokens,
                    }
                });
            }

            return aiInfo;
        }

        /// <summary>
        /// Updates an existing AiInfo instance.
        /// </summary>
        private AiInfo UpdateExistingAiInfo(AiInfo existing, long inputTokens, long outputTokens, string route)
        {
            lock (_lock)
            {
                // Update call count
                existing.Calls++;

                // Update token counts
                existing.Tokens.Input += inputTokens;
                existing.Tokens.Output += outputTokens;

                // Update route information if provided
                if (!string.IsNullOrEmpty(route))
                {
                    // Don't update route stats for now, since this is subject to change
                    // UpdateRouteStats(existing, route, inputTokens, outputTokens);
                }
            }

            return existing;
        }

        /// <summary>
        /// Updates route statistics for an AI provider.
        /// TODO: This is subject to change, so we're not using it for now
        /// </summary>
        private void UpdateRouteStats(AiInfo aiInfo, string route, long inputTokens, long outputTokens)
        {
            var routes = (List<AiRoute>)aiInfo.Routes ?? new List<AiRoute>();
            var existingRoute = routes.FirstOrDefault(r => r.Path == route);

            if (existingRoute == null)
            {
                routes.Add(new AiRoute
                {
                    Path = route,
                    Requests = 1,
                    Calls = 1,
                    Tokens = new AiTokens
                    {
                        Input = inputTokens,
                        Output = outputTokens,
                    }
                });
            }
            else
            {
                existingRoute.Requests++;
                existingRoute.Calls++;
                existingRoute.Tokens.Input += inputTokens;
                existingRoute.Tokens.Output += outputTokens;
            }

            aiInfo.Routes = routes;

            // update the total tokens for the aiInfo
            aiInfo.Tokens.Input += inputTokens;
            aiInfo.Tokens.Output += outputTokens;
        }

        /// <summary>
        /// Checks if there are no AI statistics collected.
        /// </summary>
        public bool IsEmpty() => !_aiProviders.Any();
    }
}
