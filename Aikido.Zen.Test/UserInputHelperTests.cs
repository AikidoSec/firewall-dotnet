using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using System.Collections.Generic;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Test class for UserInputHelper.
    /// </summary>
    public class UserInputHelperTests
    {
        [Test]
        public void GetSourceFromUserInputPath_ShouldReturnQuery_WhenPathStartsWithQuery()
        {
            var result = UserInputHelper.GetSourceFromUserInputPath("query.something");
            Assert.That(result, Is.EqualTo(Source.Query));
        }

        [Test]
        public void GetSourceFromUserInputPath_ShouldReturnHeaders_WhenPathStartsWithHeaders()
        {
            var result = UserInputHelper.GetSourceFromUserInputPath("headers.something");
            Assert.That(result, Is.EqualTo(Source.Headers));
        }

        [Test]
        public void GetSourceFromUserInputPath_ShouldReturnCookies_WhenPathStartsWithCookies()
        {
            var result = UserInputHelper.GetSourceFromUserInputPath("cookies.something");
            Assert.That(result, Is.EqualTo(Source.Cookies));
        }

        [Test]
        public void GetSourceFromUserInputPath_ShouldReturnRouteParams_WhenPathStartsWithRoute()
        {
            var result = UserInputHelper.GetSourceFromUserInputPath("route.something");
            Assert.That(result, Is.EqualTo(Source.RouteParams));
        }

        [Test]
        public void GetSourceFromUserInputPath_ShouldReturnBody_WhenPathDoesNotMatchAnySource()
        {
            var result = UserInputHelper.GetSourceFromUserInputPath("body.something");
            Assert.That(result, Is.EqualTo(Source.Body));
        }

        [Test]
        public void ProcessQueryParameters_ShouldAddQueryParametersToResult()
        {
            var queryParams = new Dictionary<string, string> { { "key", "value" } };
            var result = new Dictionary<string, string>();
            UserInputHelper.ProcessQueryParameters(queryParams, result);
            Assert.That(result.ContainsKey("query.key"), Is.True);
            Assert.That(result["query.key"], Is.EqualTo("value"));
        }

        [Test]
        public void ProcessHeaders_ShouldAddHeadersToResult()
        {
            var headers = new Dictionary<string, string> { { "key", "value" } };
            var result = new Dictionary<string, string>();
            UserInputHelper.ProcessHeaders(headers, result);
            Assert.That(result.ContainsKey("headers.key"), Is.True);
            Assert.That(result["headers.key"], Is.EqualTo("value"));
        }

        [Test]
        public void ProcessCookies_ShouldAddCookiesToResult()
        {
            var cookies = new Dictionary<string, string> { { "key", "value" } };
            var result = new Dictionary<string, string>();
            UserInputHelper.ProcessCookies(cookies, result);
            Assert.That(result.ContainsKey("cookies.key"), Is.True);
            Assert.That(result["cookies.key"], Is.EqualTo("value"));
        }

        [Test]
        public void IsMultipart_ShouldReturnTrueAndBoundary_WhenContentTypeIsMultipart()
        {
            var contentType = "multipart/form-data; boundary=something";
            var isMultipart = UserInputHelper.IsMultipart(contentType, out var boundary);
            Assert.That(isMultipart, Is.True);
            Assert.That(boundary, Is.EqualTo("something"));
        }

        [Test]
        public void IsMultipart_ShouldReturnFalseAndNullBoundary_WhenContentTypeIsNotMultipart()
        {
            var contentType = "application/json";
            var isMultipart = UserInputHelper.IsMultipart(contentType, out var boundary);
            Assert.That(isMultipart, Is.False);
            Assert.That(boundary, Is.Null);
        }
    }
}
