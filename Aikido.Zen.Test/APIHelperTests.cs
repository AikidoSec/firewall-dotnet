using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aikido.Zen.Test
{
    public class APIHelperTests
    {
        private Uri _baseUrl;
        private const string TestToken = "test-token";

        [SetUp]
        public void Setup()
        {
            _baseUrl = new Uri("https://api.test.com");
        }

        [Test]
        public void CreateRequest_ShouldCreateValidRequest()
        {
            // Arrange
            var path = "/test/path";
            var method = HttpMethod.Post;
            var content = new StringContent("test content", Encoding.UTF8, "application/json");

            // Act
            var request = APIHelper.CreateRequest(TestToken, _baseUrl, path, method, content);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(method));
                Assert.That(request.RequestUri, Is.EqualTo(new Uri(_baseUrl, path)));
                Assert.That(request.Headers.Authorization.Scheme, Is.EqualTo(TestToken));
                Assert.That(request.Headers.AcceptEncoding.Count, Is.EqualTo(1));
                Assert.That(request.Headers.AcceptEncoding.First().Value, Is.EqualTo("gzip"));
                Assert.That(request.Content, Is.EqualTo(content));
            });
        }

        [Test]
        public void CreateRequest_WithoutContent_ShouldCreateValidRequest()
        {
            // Arrange
            var path = "/test/path";
            var method = HttpMethod.Get;

            // Act
            var request = APIHelper.CreateRequest(TestToken, _baseUrl, path, method);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(method));
                Assert.That(request.RequestUri, Is.EqualTo(new Uri(_baseUrl, path)));
                Assert.That(request.Headers.Authorization.Scheme, Is.EqualTo(TestToken));
                Assert.That(request.Content, Is.Null);
            });
        }

        [Test]
        public void ToAPIResponse_RateLimited_ShouldReturnRateLimitedResponse()
        {
            // Arrange
            var response = new HttpResponseMessage((HttpStatusCode)429);

            // Act
            var result = APIHelper.ToAPIResponse<TestAPIResponse>(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("rate_limited"));
            });
        }

        [Test]
        public void ToAPIResponse_Unauthorized_ShouldReturnUnauthorizedResponse()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);

            // Act
            var result = APIHelper.ToAPIResponse<TestAPIResponse>(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("invalid_token"));
            });
        }

        [Test]
        public void ToAPIResponse_SuccessfulResponse_ShouldDeserializeContent()
        {
            // Arrange
            var testData = new TestAPIResponse { TestProperty = "test value" };
            var jsonContent = JsonSerializer.Serialize(testData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            // Act
            var result = APIHelper.ToAPIResponse<TestAPIResponse>(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.TestProperty, Is.EqualTo(testData.TestProperty));
                Assert.That(result.Error, Is.Null);
            });
        }

        [Test]
        public void ToAPIResponse_InvalidJson_ShouldReturnErrorResponse()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
            };

            // Act
            var result = APIHelper.ToAPIResponse<TestAPIResponse>(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("unknown_error"));
            });
        }

        [Test]
        public void ToAPIResponse_UnexpectedStatusCode_ShouldReturnErrorResponse()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

            // Act
            var result = APIHelper.ToAPIResponse<TestAPIResponse>(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("unknown_error"));
            });
        }

        [Test]
        public void CreateRequest_WithNullBaseUrl_ShouldThrowArgumentNullException()
        {
            // Arrange
            Uri nullBaseUrl = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                APIHelper.CreateRequest(TestToken, nullBaseUrl, "/test", HttpMethod.Get));
        }

        [Test]
        public void CreateRequest_WithNullToken_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                APIHelper.CreateRequest(null, _baseUrl, "/test", HttpMethod.Get));
        }

        private class TestAPIResponse : APIResponse
        {
            public string TestProperty { get; set; }
        }
    }
}