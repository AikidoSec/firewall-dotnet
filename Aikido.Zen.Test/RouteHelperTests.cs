using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System;

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

        
    }
}
