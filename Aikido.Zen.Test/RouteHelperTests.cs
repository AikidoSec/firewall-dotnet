using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test.Helpers
{
    internal class RouteHelperTests
    {
        [TestCase("{parameter}", true)]
        [TestCase("{parameter", false)]
        [TestCase("parameter}", false)]
        [TestCase("parameter", false)]
        [TestCase("thing={value}", false)]
        public void IsRouteParameter_ShouldReturnExpectedResult(string input, bool expectedResult)
        {
            // Arrange
            var inputSpan = input.AsSpan();

            // Act
            var result = inputSpan.IsRouteParameter();

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }

        [TestCase("/api/{id}", "/api/123", true)]
        [TestCase("/api/{id}", "/api/", false)]
        [TestCase("/api/{id}", "/api/123/extra", false)]
        [TestCase("/api/{id}/details", "/api/123/details", true)]
        [TestCase("/api/{id}/details", "/api/123/other", false)]
        [TestCase("/api/{id}/details/{subId}", "/api/123/details/456", true)]
        [TestCase("/api/{id}/details/{subId}", "/api/123/details/", false)]
        [TestCase("/api/{id}/details/{subId}", "/api/123/details/456/extra", false)]
        [TestCase("/api/{id}/details/{subId}/info", "/api/123/details/456/info", true)]
        [TestCase("/api/{id}/details/{subId}/info", "/api/123/details/456/other", false)]
        [TestCase("/api/{id}/details/{subId}/info?query=1", "/api/123/details/456/info?query=1", true)] // Query parameter handling
        [TestCase("/api/{id}/details/{subId}/info?query=1", "/api/123/details/456/info?query=2", true)] // Query parameter handling
        [TestCase("/API/{ID}/DETAILS/{SUBID}", "/api/123/details/456", true)] // Case sensitivity
        [TestCase("/api/{id}/details/{subId}/info", "/api/123/details/456/info#section", true)] // Special character handling
        [TestCase("/api/{id}/details/{subId}/info", "/api/123/details/456/info?query=1#section", true)] // Special character handling
        [TestCase("invalid:route.?a?=&", "/api/123/details/456/info?query=1#section", false)] // Special character handling
        [TestCase("path/to/file.txt", "path/to/file.txt", true)]
        [TestCase("path/to/file.txt", "path/to/file.txt/extra", false)]
        [TestCase("path/to/file.txt", "path/to/file.txt?query=1", true)]
        [TestCase("path/to/file.txt", "path/to/file.txt#section", true)]
        [TestCase("path/to/file.txt", "path/to/file.txt?query=1#section", true)]

        public void MatchRoute_ShouldReturnExpectedResult(string pattern, string path, bool expectedResult)
        {
            // Act
            var result = RouteHelper.MatchRoute(pattern, path);

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }

        [TestCase("/api/resource", "GET", 200, true)]
        [TestCase("/api/resource", "OPTIONS", 200, false)]
        [TestCase("/api/resource", "GET", 404, false)]
        [TestCase("/.well-known", "GET", 200, false)]
        [TestCase("/.well-known/change-password", "GET", 200, true)]
        [TestCase("/.well-known/security.txt", "GET", 200, false)]
        [TestCase("/cgi-bin/luci/;stok=/locale", "GET", 200, false)]
        [TestCase("/whatever/cgi-bin", "GET", 200, false)]
        [TestCase("/api/.hidden/resource", "GET", 200, false)]
        [TestCase("/api/resource.php", "GET", 200, false)]
        [TestCase("/test.webmanifest", "GET", 200, false)]
        [TestCase("/api/test.config", "GET", 200, false)]
        [TestCase("/test.properties", "GET", 200, false)]
        [TestCase("/api/resource", "HEAD", 200, false)]
        [TestCase("/api/resource", "GET", 500, false)]
        public void ShouldAddRoute_ShouldReturnExpectedResult(string route, string method, int statusCode, bool expectedResult)
        {
            // Arrange
            var context = new Context { Route = route, Method = method };

            // Act
            var result = RouteHelper.ShouldAddRoute(context, statusCode);

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }


        /// <summary>
        /// Tests for ShouldAddRoute method when context is null.
        /// </summary>
        [Test]
        public void ShouldAddRoute_ContextIsNull_ShouldReturnFalse()
        {
            // Act
            var result = RouteHelper.ShouldAddRoute(null, 200);

            // Assert
            Assert.That(result, Is.False);
        }

        /// <summary>
        /// Tests for ShouldAddRoute method when route is null.
        /// </summary>
        [TestCase(null, "GET", 200, false)]
        [TestCase(null, "OPTIONS", 200, false)]
        [TestCase(null, "GET", 404, false)]
        public void ShouldAddRoute_RouteIsNull_ShouldReturnExpectedResult(string? route, string method, int statusCode, bool expectedResult)
        {
            // Arrange
            var context = new Context { Route = route, Method = method };

            // Act
            var result = RouteHelper.ShouldAddRoute(context, statusCode);

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }

        [TestCase("", "", true)] // Both pattern and path are empty
        [TestCase("/api/resource/", "/api/resource", true)] // Trailing slash in pattern
        [TestCase("/api/resource", "/api/resource/", true)] // Trailing slash in path
        [TestCase("/api/RESOURCE", "/api/resource", true)] // Case sensitivity
        [TestCase("/api/{id}/details!", "/api/123/details!", true)] // Special characters in path
        public void MatchRoute_EdgeCases_ShouldReturnExpectedResult(string pattern, string path, bool expectedResult)
        {
            // Act
            var result = RouteHelper.MatchRoute(pattern, path);

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }

        [TestCase(null, "GET", 200, false)] // Null method
        [TestCase("/api/resource", null, 200, false)] // Null route
        [TestCase("/api/resource", "GET", 199, false)] // Invalid status code below 200
        [TestCase("/api/resource", "GET", 400, false)] // Invalid status code above 399
        [TestCase("/.hidden/resource", "GET", 200, false)] // Dot file not .well-known
        [TestCase("/api/cgi-bin/resource", "GET", 200, false)] // Ignored string in route
        public void ShouldAddRoute_EdgeCases_ShouldReturnExpectedResult(string? route, string? method, int statusCode, bool expectedResult)
        {
            // Arrange
            var context = new Context { Route = route, Method = method };

            // Act
            var result = RouteHelper.ShouldAddRoute(context, statusCode);

            // Assert
            Assert.That(expectedResult, Is.EqualTo(result));
        }

        [Test]
        public void MatchEndpoints_InvalidUrlAndNoRoute_ShouldReturnEmptyList()
        {
            // Arrange
            var context = new Context { Method = "POST", Url = "abc", Route = null };
            var endpoints = new List<EndpointConfig>();

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MatchEndpoints_NoUrlAndNoRoute_ShouldReturnEmptyList()
        {
            // Arrange
            var context = new Context { Method = "POST", Url = null, Route = null };
            var endpoints = new List<EndpointConfig>();

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MatchEndpoints_NoMethod_ShouldReturnEmptyList()
        {
            // Arrange
            var context = new Context { Method = null, Url = "http://localhost:4000/posts/3", Route = "/posts/3" };
            var endpoints = new List<EndpointConfig>();

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MatchEndpoints_NoMatches_ShouldReturnEmptyList()
        {
            // Arrange
            var context = new Context { Method = "POST", Url = "http://localhost:4000/posts/3", Route = "/posts/3" };
            var endpoints = new List<EndpointConfig>();

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MatchEndpoints_ExactRouteMatch_ShouldReturnMatchingEndpoint()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/posts/3",
                Route = "/posts/:number"
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "POST",
                    Route = "/posts/:number",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("POST"));
            Assert.That(result[0].Route, Is.EqualTo("/posts/:number"));
            Assert.That(result[0].RateLimiting.Enabled, Is.True);
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(10));
            Assert.That(result[0].RateLimiting.WindowSizeInMS, Is.EqualTo(1000));
            Assert.That(result[0].ForceProtectionOff, Is.False);
        }

        [Test]
        public void MatchEndpoints_RelativeUrl_ShouldMatchCorrectly()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "/posts/3",
                Route = "/posts/:number"
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "POST",
                    Route = "/posts/:number",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("POST"));
            Assert.That(result[0].Route, Is.EqualTo("/posts/:number"));
            Assert.That(result[0].RateLimiting.Enabled, Is.True);
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(10));
            Assert.That(result[0].RateLimiting.WindowSizeInMS, Is.EqualTo(1000));
            Assert.That(result[0].ForceProtectionOff, Is.False);
        }

        [Test]
        public void MatchEndpoints_WildcardRoute_ShouldMatchCorrectly()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/posts/3",
                Route = null
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "*",
                    Route = "/posts/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("*"));
            Assert.That(result[0].Route, Is.EqualTo("/posts/*"));
            Assert.That(result[0].RateLimiting.Enabled, Is.True);
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(10));
            Assert.That(result[0].RateLimiting.WindowSizeInMS, Is.EqualTo(1000));
            Assert.That(result[0].ForceProtectionOff, Is.False);
        }

        [Test]
        public void MatchEndpoints_WildcardWithRelativeUrl_ShouldMatchCorrectly()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "/posts/3",
                Route = null
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "*",
                    Route = "/posts/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("*"));
            Assert.That(result[0].Route, Is.EqualTo("/posts/*"));
            Assert.That(result[0].RateLimiting.Enabled, Is.True);
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(10));
            Assert.That(result[0].RateLimiting.WindowSizeInMS, Is.EqualTo(1000));
            Assert.That(result[0].ForceProtectionOff, Is.False);
        }

        [Test]
        public void MatchEndpoints_MultipleWildcards_ShouldFavorMoreSpecific()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/posts/3/comments/10",
                Route = null
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "*",
                    Route = "/posts/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                },
                new()
                {
                    Method = "*",
                    Route = "/posts/*/comments/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Route, Is.EqualTo("/posts/*/comments/*")); // More specific first
            Assert.That(result[1].Route, Is.EqualTo("/posts/*")); // Less specific second
        }

        [Test]
        public void MatchEndpoints_SpecificMethodWithWildcardRoute_ShouldMatch()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/posts/3/comments/10",
                Route = null
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "POST",
                    Route = "/posts/*/comments/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 10,
                        WindowSizeInMS = 1000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("POST"));
            Assert.That(result[0].Route, Is.EqualTo("/posts/*/comments/*"));
            Assert.That(result[0].RateLimiting.Enabled, Is.True);
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(10));
            Assert.That(result[0].RateLimiting.WindowSizeInMS, Is.EqualTo(1000));
            Assert.That(result[0].ForceProtectionOff, Is.False);
        }

        [Test]
        public void MatchEndpoints_SpecificRouteOverWildcard_ShouldTakePrecedence()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/api/coach",
                Route = "/api/coach"
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "*",
                    Route = "/api/*",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 20,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                },
                new()
                {
                    Method = "POST",
                    Route = "/api/coach",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 100,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Method, Is.EqualTo("POST")); // Specific method first
            Assert.That(result[0].Route, Is.EqualTo("/api/coach")); // Specific route first
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(100));
            Assert.That(result[1].Method, Is.EqualTo("*")); // Wildcard method second
            Assert.That(result[1].Route, Is.EqualTo("/api/*")); // Wildcard route second
            Assert.That(result[1].RateLimiting.MaxRequests, Is.EqualTo(20));
        }

        [Test]
        public void MatchEndpoints_SpecificMethodOverWildcard_ShouldTakePrecedence()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/api/test",
                Route = "/api/test"
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "*",
                    Route = "/api/test",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 20,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                },
                new()
                {
                    Method = "POST",
                    Route = "/api/test",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 100,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("POST")); // Only specific method should be returned
            Assert.That(result[0].Route, Is.EqualTo("/api/test"));
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(100));
        }

        [Test]
        public void MatchEndpoints_SpecificMethodOverWildcard_ShouldTakePrecedence_ReversedOrder()
        {
            // Arrange
            var context = new Context
            {
                Method = "POST",
                Url = "http://localhost:4000/api/test",
                Route = "/api/test"
            };
            var endpoints = new List<EndpointConfig>
            {
                new()
                {
                    Method = "POST",
                    Route = "/api/test",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 100,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                },
                new()
                {
                    Method = "*",
                    Route = "/api/test",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 20,
                        WindowSizeInMS = 60000
                    },
                    ForceProtectionOff = false
                }
            };

            // Act
            var result = RouteHelper.MatchEndpoints(context, endpoints);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Method, Is.EqualTo("POST")); // Only specific method should be returned
            Assert.That(result[0].Route, Is.EqualTo("/api/test"));
            Assert.That(result[0].RateLimiting.MaxRequests, Is.EqualTo(100));
        }
    }
}
