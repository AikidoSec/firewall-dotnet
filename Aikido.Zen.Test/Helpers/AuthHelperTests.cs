using NUnit.Framework;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class AuthHelperTests
    {
        [Test]
        public void MergeApiAuthTypes_MergesCorrectly()
        {
            var existing = new List<APIAuthType>
            {
                new APIAuthType { Type = "http", Scheme = "bearer" },
                new APIAuthType { Type = "apiKey", In = "header", Name = "x-api-key" }
            };

            var newAuth = new List<APIAuthType>
            {
                new APIAuthType { Type = "http", Scheme = "bearer" },
                new APIAuthType { Type = "http", Scheme = "basic" },
                new APIAuthType { Type = "apiKey", In = "header", Name = "x-api-key-v2" }
            };

            var expected = new List<APIAuthType>
            {
                new APIAuthType { Type = "http", Scheme = "bearer" },
                new APIAuthType { Type = "apiKey", In = "header", Name = "x-api-key" },
                new APIAuthType { Type = "http", Scheme = "basic" },
                new APIAuthType { Type = "apiKey", In = "header", Name = "x-api-key-v2" }
            };

            var result = AuthHelper.MergeApiAuthTypes(existing, newAuth);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void MergeApiAuthTypes_WithNullInputs_ReturnsNull()
        {
            var result = AuthHelper.MergeApiAuthTypes(null, null);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void MergeApiAuthTypes_WithExistingOnly_ReturnsExisting()
        {
            var existing = new List<APIAuthType>
            {
                new APIAuthType { Type = "http", Scheme = "bearer" }
            };

            var result = AuthHelper.MergeApiAuthTypes(existing, null);

            Assert.That(result, Is.EqualTo(existing));
        }

        [Test]
        public void MergeApiAuthTypes_WithNewOnly_ReturnsNew()
        {
            var newAuth = new List<APIAuthType>
            {
                new APIAuthType { Type = "http", Scheme = "digest" }
            };

            var result = AuthHelper.MergeApiAuthTypes(null, newAuth);

            Assert.That(result, Is.EqualTo(newAuth));
        }
    }
}
