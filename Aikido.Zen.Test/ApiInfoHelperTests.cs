using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers.OpenAPI;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ApiInfoHelperTests
    {
        private Context CreateTestContext(
            Dictionary<string, string[]>? headers = null,
            object? body = null,
            Dictionary<string, string[]>? query = null)
        {
            string? contentType = headers?.GetValueOrDefault("content-type")?.FirstOrDefault() ?? "application/json";
            Stream? bodyStream = null;

            if (body != null)
            {
                switch (contentType.ToLower())
                {
                    case "application/json":
                        var jsonString = JsonSerializer.Serialize(body);
                        bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
                        break;

                    case "application/x-www-form-urlencoded":
                        if (body is Dictionary<string, object> formData)
                        {
                            var formParams = formData.Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value?.ToString())}");
                            var formString = string.Join("&", formParams);
                            bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(formString));
                        }
                        break;

                    case "application/xml":
                        if (body is string xmlString)
                        {
                            bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(xmlString));
                        }
                        break;

                    case "multipart/form-data":
                        if (body is Dictionary<string, object> multipartData)
                        {
                            var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
                            var formBuilder = new StringBuilder();
                            foreach (var kvp in multipartData)
                            {
                                formBuilder.AppendLine($"--{boundary}");
                                formBuilder.AppendLine($"Content-Disposition: form-data; name=\"{kvp.Key}\"");
                                formBuilder.AppendLine();
                                formBuilder.AppendLine(kvp.Value?.ToString());
                            }
                            formBuilder.AppendLine($"--{boundary}--");
                            bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(formBuilder.ToString()));

                            // Update content type with boundary
                            headers ??= new Dictionary<string, string[]>();
                            headers["content-type"] = new[] { $"multipart/form-data; boundary={boundary}" };
                        }
                        break;
                }
            }

            var queryParams = query?.ToDictionary(x => x.Key, x => string.Join(",", x.Value)) ?? new Dictionary<string, string>();
            var headerDict = headers?.ToDictionary(x => x.Key, x => string.Join(",", x.Value)) ?? new Dictionary<string, string>();
            var parsed = HttpHelper.ReadAndFlattenHttpDataAsync(queryParams, headerDict, new Dictionary<string, string>(), bodyStream, contentType, bodyStream?.Length ?? 0).Result;

            return new Context
            {
                Method = "POST",
                Route = "/test",
                Headers = headers ?? new Dictionary<string, string[]>(),
                Body = bodyStream,
                Query = query ?? new Dictionary<string, string[]>(),
                RemoteAddress = "127.0.0.1",
                Url = "http://localhost/test",
                RouteParams = new Dictionary<string, string>(),
                Cookies = new Dictionary<string, string>(),
                Source = "test",
                ParsedBody = parsed.ParsedBody,
                ParsedUserInput = parsed.FlattenedData
            };
        }

        [Test]
        public void GetApiInfo_WithJsonBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new Dictionary<string, object>
                {
                    { "name", "test" },
                    { "age", 25 },
                    { "email", "test@example.com" }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("json"));
            Assert.That(result.Body.Schema.Properties!["name"].Type[0], Is.EqualTo("string"));
            Assert.That(result.Body.Schema.Properties["age"].Type[0], Is.EqualTo("number"));
            Assert.That(result.Body.Schema.Properties["email"].Type[0], Is.EqualTo("string"));
            Assert.That(result.Body.Schema.Properties["email"].Format, Is.EqualTo("email"));
        }

        [Test]
        public void GetApiInfo_WithQueryParameters_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                query: new Dictionary<string, string[]>
                {
                    { "page", ["1"] },
                    { "limit", ["10"] },
                    { "search", ["test"] }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Query, Is.Not.Null);
            Assert.That(result.Query!.Properties!["page"].Format, Is.Null);
            Assert.That(result.Query.Properties["limit"].Format, Is.Null);
            Assert.That(result.Query.Properties["search"].Format, Is.Null);
        }

        [Test]
        public void GetApiInfo_WithAuth_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "authorization", ["Bearer token123"] }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Auth, Is.Not.Null);
            Assert.That(result.Auth![0].Type, Is.EqualTo("http"));
            Assert.That(result.Auth[0].Scheme, Is.EqualTo("bearer"));
        }

        [Test]
        public void GetApiInfo_WithComplexNestedBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new Dictionary<string, object>
                {
                    {
                        "user", new Dictionary<string, object>
                        {
                            { "name", "John Doe" },
                            { "age", 30 },
                            {
                                "address", new Dictionary<string, object>
                                {
                                    { "street", "123 Main St" },
                                    { "city", "Example City" },
                                    { "zipCode", "12345" }
                                }
                            }
                        }
                    }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("json"));

            var userSchema = result.Body.Schema.Properties!["user"];
            Assert.That(userSchema.Type[0], Is.EqualTo("object"));
            Assert.That(userSchema.Properties!["name"].Type[0], Is.EqualTo("string"));
            Assert.That(userSchema.Properties["age"].Type[0], Is.EqualTo("number"));

            var addressSchema = userSchema.Properties["address"];
            Assert.That(addressSchema.Type[0], Is.EqualTo("object"));
            Assert.That(addressSchema.Properties!["street"].Type[0], Is.EqualTo("string"));
            Assert.That(addressSchema.Properties["zipCode"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void GetApiInfo_WithArrayBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new[]
                {
                    new Dictionary<string, object>
                    {
                        { "id", 1 },
                        { "name", "Item 1" }
                    },
                    new Dictionary<string, object>
                    {
                        { "id", 2 },
                        { "name", "Item 2" }
                    }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("json"));
            Assert.That(result.Body.Schema.Type[0], Is.EqualTo("array"));

            var itemSchema = result.Body.Schema.Items;
            Assert.That(itemSchema!.Type[0], Is.EqualTo("object"));
            Assert.That(itemSchema.Properties!["id"].Type[0], Is.EqualTo("number"));
            Assert.That(itemSchema.Properties["name"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void GetApiInfo_WithNoContentTypeAndEmptyBody_ReturnsNull()
        {
            var context = CreateTestContext();
            var result = OpenAPIHelper.GetApiInfo(context);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetApiInfo_WithFormUrlEncodedBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/x-www-form-urlencoded"] }
                },
                body: new Dictionary<string, object>
                {
                    { "username", "testuser" },
                    { "password", "testpass" }
                }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("form-urlencoded"));
            Assert.That(result.Body.Schema.Properties!["username"].Type[0], Is.EqualTo("string"));
            Assert.That(result.Body.Schema.Properties["password"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void GetApiInfo_WithPrimitiveArrayBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new[] { "item1", "item2", "item3" }
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("json"));
            Assert.That(result.Body.Schema.Type[0], Is.EqualTo("array"));
            Assert.That(result.Body.Schema.Items!.Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void GetApiInfo_WithPrimitiveBody_ReturnsCorrectSpec()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: "simple string value"
            );

            var result = OpenAPIHelper.GetApiInfo(context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Body!.Type, Is.EqualTo("json"));
            Assert.That(result.Body.Schema.Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void UpdateApiInfo_SuccessfulUpdate_UpdatesRouteCorrectly()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new Dictionary<string, object>
                {
                    { "name", "test" }
                }
            );

            var existingRoute = new Route
            {
                ApiSpec = new APISpec
                {
                    Body = new APIBodyInfo
                    {
                        Type = "json",
                        Schema = new DataSchema
                        {
                            Properties = new Dictionary<string, DataSchema>
                            {
                                { "name", new DataSchema { Type = new[] { "string" } } }
                            }
                        }
                    }
                },
                Hits = 0
            };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Body, Is.Not.Null);
            Assert.That(existingRoute.ApiSpec.Body.Type, Is.EqualTo("json"));
            Assert.That(existingRoute.ApiSpec.Body.Schema.Properties["name"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void UpdateApiInfo_ExceedingMaxSamples_DoesNotUpdate()
        {
            var context = CreateTestContext();
            var existingRoute = new Route { Hits = 11 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec, Is.Null);
        }

        [Test]
        public void UpdateApiInfo_NullNewInfo_DoesNotUpdate()
        {
            var context = CreateTestContext();
            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Body, Is.Null);
            Assert.That(existingRoute.ApiSpec.Query, Is.Null);
            Assert.That(existingRoute.ApiSpec.Auth, Is.Null);
        }

        [Test]
        public void UpdateApiInfo_ErrorHandling_ExitsGracefully()
        {
            var context = CreateTestContext();
            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            // Simulate an error by passing a null context
            ApiInfoHelper.UpdateApiInfo(null, existingRoute, 10);

            // Ensure no exception is thrown and the route remains unchanged
            Assert.That(existingRoute.ApiSpec.Body, Is.Null);
            Assert.That(existingRoute.ApiSpec.Query, Is.Null);
            Assert.That(existingRoute.ApiSpec.Auth, Is.Null);
        }

        [Test]
        public void UpdateApiInfo_NewBodyInfo_UpdatesBodyCorrectly()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "content-type", ["application/json"] }
                },
                body: new Dictionary<string, object>
                {
                    { "newField", "newValue" }
                }
            );

            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Body, Is.Not.Null);
            Assert.That(existingRoute.ApiSpec.Body.Type, Is.EqualTo("json"));
            Assert.That(existingRoute.ApiSpec.Body.Schema.Properties["newField"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void UpdateApiInfo_NewQueryInfo_UpdatesQueryCorrectly()
        {
            var context = CreateTestContext(
                query: new Dictionary<string, string[]>
                {
                    { "newQuery", ["newValue"] }
                }
            );

            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Query, Is.Not.Null);
            Assert.That(existingRoute.ApiSpec.Query.Properties["newQuery"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void UpdateApiInfo_NewAuthInfo_UpdatesAuthCorrectly()
        {
            var context = CreateTestContext(
                headers: new Dictionary<string, string[]>
                {
                    { "authorization", ["Bearer newToken"] }
                }
            );

            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Auth, Is.Not.Null);
            Assert.That(existingRoute.ApiSpec.Auth[0].Type, Is.EqualTo("http"));
            Assert.That(existingRoute.ApiSpec.Auth[0].Scheme, Is.EqualTo("bearer"));
        }

        [Test]
        public void UpdateApiInfo_NormalizeEmptySpec_SetsEmptyApiSpec()
        {
            var context = CreateTestContext();
            var existingRoute = new Route { ApiSpec = new APISpec(), Hits = 0 };

            ApiInfoHelper.UpdateApiInfo(context, existingRoute, 10);

            Assert.That(existingRoute.ApiSpec.Body, Is.Null);
            Assert.That(existingRoute.ApiSpec.Query, Is.Null);
            Assert.That(existingRoute.ApiSpec.Auth, Is.Null);
        }
    }
}
