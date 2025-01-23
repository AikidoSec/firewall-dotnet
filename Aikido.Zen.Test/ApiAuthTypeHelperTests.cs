using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers.OpenAPI;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ApiAuthTypeHelperTests
    {
        private Context CreateTestContext(Dictionary<string, string[]>? headers = null, Dictionary<string, string>? cookies = null)
        {
            return new Context
            {
                Method = "GET",
                Route = "/test",
                Headers = headers ?? new Dictionary<string, string[]>(),
                Body = null,
                RemoteAddress = "",
                Url = "http://localhost/test",
                RouteParams = new Dictionary<string, string>(),
                Query = new Dictionary<string, string[]>(),
                Cookies = cookies ?? new Dictionary<string, string>(),
                Source = "test"
            };
        }

        [Test]
        public void GetApiAuthType_WithBearerToken_ReturnsHttpBearer()
        {
            var context = CreateTestContext(new Dictionary<string, string[]>
            {
                { "authorization", ["Bearer token123"] }
            });

            var result = OpenAPIHelper.GetApiAuthType(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].Type, Is.EqualTo("http"));
            Assert.That(result[0].Scheme, Is.EqualTo("bearer"));
        }

        [Test]
        public void GetApiAuthType_WithBasicAuth_ReturnsHttpBasic()
        {
            var context = CreateTestContext(new Dictionary<string, string[]>
            {
                { "authorization", ["Basic base64encoded"] }
            });

            var result = ApiAuthTypeHelper.GetApiAuthType(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].Type, Is.EqualTo("http"));
            Assert.That(result[0].Scheme, Is.EqualTo("basic"));
        }

        [Test]
        public void GetApiAuthType_WithApiKey_ReturnsApiKeyType()
        {
            var context = CreateTestContext(new Dictionary<string, string[]>
            {
                { "x-api-key", ["apikey123"] }
            });

            var result = ApiAuthTypeHelper.GetApiAuthType(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].Type, Is.EqualTo("apiKey"));
            Assert.That(result[0].In, Is.EqualTo("header"));
            Assert.That(result[0].Name, Is.EqualTo("x-api-key"));
        }

        [Test]
        public void GetApiAuthType_WithAuthCookie_ReturnsApiKeyType()
        {
            var context = CreateTestContext(
                cookies: new Dictionary<string, string> { { "session", "sessiontoken123" } }
            );

            var result = ApiAuthTypeHelper.GetApiAuthType(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].Type, Is.EqualTo("apiKey"));
            Assert.That(result[0].In, Is.EqualTo("cookie"));
            Assert.That(result[0].Name, Is.EqualTo("session"));
        }

        [Test]
        public void GetApiAuthType_WithNoAuth_ReturnsNull()
        {
            var context = CreateTestContext();
            var result = ApiAuthTypeHelper.GetApiAuthType(context);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetApiAuthType_WithMultipleAuth_ReturnsAllTypes()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "authorization", ["Bearer token123"] },
                    { "x-api-key", ["apikey123"] }
                },
                cookies: new Dictionary<string, string>
                {
                    { "session", "sessiontoken123" }
                }
            );

            var result = ApiAuthTypeHelper.GetApiAuthType(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));

            // Check Bearer token
            Assert.That(result.Exists(x => x.Type == "http" && x.Scheme == "bearer"));

            // Check API Key
            Assert.That(result.Exists(x => x.Type == "apiKey" && x.In == "header" && x.Name == "x-api-key"));

            // Check Cookie
            Assert.That(result.Exists(x => x.Type == "apiKey" && x.In == "cookie" && x.Name == "session"));
        }
    }
}
