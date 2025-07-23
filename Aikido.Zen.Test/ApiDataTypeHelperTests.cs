using System.Collections.Generic;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;
using NUnit.Framework;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class ApiDataTypeHelperTests
    {
        [Test]
        public void GetBodyDataType_WithJsonContentType_ReturnsJson()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/json" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("json"));
        }

        [Test]
        public void GetBodyDataType_WithVendorJsonContentType_ReturnsJson()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/vnd.api+json" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("json"));
        }

        [Test]
        public void GetBodyDataType_WithFormUrlEncoded_ReturnsFormUrlEncoded()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/x-www-form-urlencoded" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("form-urlencoded"));
        }

        [Test]
        public void GetBodyDataType_WithFormData_ReturnsFormData()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "multipart/form-data" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("form-data"));
        }

        [Test]
        public void GetBodyDataType_WithXml_ReturnsXml()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/xml" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("xml"));
        }

        [Test]
        public void GetBodyDataType_WithTextXml_ReturnsXml()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "text/xml" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("xml"));
        }

        [Test]
        public void GetBodyDataType_WithCharset_ReturnsCorrectType()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/json; charset=utf-8" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("json"));
        }

        [Test]
        public void GetBodyDataType_WithNoContentType_ReturnsNull()
        {
            var headers = new Dictionary<string, string>();
            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetBodyDataType_WithUnknownContentType_ReturnsNull()
        {
            var headers = new Dictionary<string, string>
            {
                { "content-type", "application/unknown" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetBodyDataType_WithMixedCase_ReturnsCorrectType()
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "APPLICATION/JSON" }
            };

            var result = OpenAPIHelper.GetBodyDataType(headers);
            Assert.That(result, Is.EqualTo("json"));
        }


    }
}
