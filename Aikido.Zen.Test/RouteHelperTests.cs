using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System;

namespace Aikido.Zen.Test
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
            Assert.AreEqual(expectedResult, result);
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
        public void MatchRoute_ShouldReturnExpectedResult(string pattern, string path, bool expectedResult)
        {
            // Act
            var result = RouteHelper.MatchRoute(pattern, path);

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
