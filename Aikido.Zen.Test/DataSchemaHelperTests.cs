using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using Aikido.Zen.Core.Helpers.OpenAPI;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class DataSchemaHelperTests
    {
        [Test]
        public void GetDataSchema_WithSimpleTypes_ReturnsCorrectSchema()
        {
            // Test string
            var stringSchema = OpenAPIHelper.GetDataSchema("test");
            Assert.That(stringSchema.Type[0], Is.EqualTo("string"));

            // Test array of strings
            var arraySchema = OpenAPIHelper.GetDataSchema(new[] { "test" });
            Assert.That(arraySchema.Type[0], Is.EqualTo("array"));
            Assert.That(arraySchema.Items!.Type[0], Is.EqualTo("string"));

            // Test simple object
            var objectSchema = OpenAPIHelper.GetDataSchema(new Dictionary<string, object>
            {
                { "test", "abc" }
            });
            Assert.That(objectSchema.Type[0], Is.EqualTo("object"));
            Assert.That(objectSchema.Properties!["test"].Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void GetDataSchema_WithComplexObject_ReturnsCorrectSchema()
        {
            var data = new Dictionary<string, object>
            {
                { "test", 123 },
                { "arr", new[] { 1, 2, 3 } }
            };

            var schema = OpenAPIHelper.GetDataSchema(data);

            Assert.That(schema.Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties!["test"].Type[0], Is.EqualTo("number"));
            Assert.That(schema.Properties["arr"].Type[0], Is.EqualTo("array"));
            Assert.That(schema.Properties["arr"].Items!.Type[0], Is.EqualTo("number"));
        }

        [Test]
        public void GetDataSchema_WithNestedObjects_ReturnsCorrectSchema()
        {
            var data = new Dictionary<string, object>
            {
                { "test", 123 },
                {
                    "arr", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "sub", true }
                        }
                    }
                },
                { "x", null }
            };

            var schema = OpenAPIHelper.GetDataSchema(data);

            Assert.That(schema.Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties!["test"].Type[0], Is.EqualTo("number"));
            Assert.That(schema.Properties["arr"].Type[0], Is.EqualTo("array"));
            Assert.That(schema.Properties["arr"].Items!.Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties["arr"].Items.Properties!["sub"].Type[0], Is.EqualTo("boolean"));
            Assert.That(schema.Properties["x"].Type[0], Is.EqualTo("null"));
        }

        [Test]
        public void GetDataSchema_WithDeeplyNestedObject_ReturnsCorrectSchema()
        {
            var data = new Dictionary<string, object>
            {
                {
                    "test", new Dictionary<string, object>
                    {
                        {
                            "x", new Dictionary<string, object>
                            {
                                {
                                    "y", new Dictionary<string, object>
                                    {
                                        { "z", 123 }
                                    }
                                }
                            }
                        }
                    }
                },
                { "arr", new object[] { } }
            };

            var schema = OpenAPIHelper.GetDataSchema(data);

            Assert.That(schema.Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties!["test"].Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties["test"].Properties!["x"].Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties["test"].Properties["x"].Properties!["y"].Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties["test"].Properties["x"].Properties["y"].Properties!["z"].Type[0], Is.EqualTo("number"));
            Assert.That(schema.Properties["arr"].Type[0], Is.EqualTo("array"));
            Assert.That(schema.Properties["arr"].Items, Is.Null);
        }

        [Test]
        public void GetDataSchema_WithFormats_ReturnsCorrectSchema()
        {
            var data = new Dictionary<string, object>
            {
                { "e", "test@example.com" },
                { "i", "127.0.0.1" },
                { "u", "http://example.com" },
                { "d", "2024-10-14" }
            };

            var schema = OpenAPIHelper.GetDataSchema(data);

            Assert.That(schema.Type[0], Is.EqualTo("object"));
            Assert.That(schema.Properties!["e"].Type[0], Is.EqualTo("string"));
            Assert.That(schema.Properties["e"].Format, Is.EqualTo("email"));
            Assert.That(schema.Properties["i"].Type[0], Is.EqualTo("string"));
            Assert.That(schema.Properties["i"].Format, Is.Null);
            Assert.That(schema.Properties["u"].Type[0], Is.EqualTo("string"));
            Assert.That(schema.Properties["u"].Format, Is.EqualTo("uri"));
            Assert.That(schema.Properties["d"].Type[0], Is.EqualTo("string"));
            Assert.That(schema.Properties["d"].Format, Is.EqualTo("date"));
        }

        [Test]
        public void GetDataSchema_WithMaxDepth_StopsAtLimit()
        {

            // Test within max depth
            var obj1 = GenerateTestObjectWithDepth(10);
            var schema1 = OpenAPIHelper.GetDataSchema(obj1);

            // Test exceeding max depth
            var obj2 = GenerateTestObjectWithDepth(21);
            var schema2 = OpenAPIHelper.GetDataSchema(obj2);

            Assert.That(schema1.ToString(), Does.Contain("\"type\":\"string\""));
            Assert.That(schema2.ToString(), Does.Not.Contain("\"type\":\"string\""));
        }

        [Test]
        public void GetDataSchema_WithMaxProperties_LimitsPropertyCount()
        {
            // Test within max properties
            var obj1 = GenerateTestObjectWithProperties(80);
            var schema1 = OpenAPIHelper.GetDataSchema(obj1);

            // Test exceeding max properties
            var obj2 = GenerateTestObjectWithProperties(120);
            var schema2 = OpenAPIHelper.GetDataSchema(obj2);

            Assert.That(schema1.Properties!.Count, Is.EqualTo(80));
            Assert.That(schema2.Properties!.Count, Is.EqualTo(100));
        }

        private object GenerateTestObjectWithDepth(int depth)
            {
            if (depth == 0)
                return "testValue";

            return new Dictionary<string, object>
                {
                    { "prop", GenerateTestObjectWithDepth(depth - 1) }
                };
        }

        private object GenerateTestObjectWithProperties(int count)
        {
            var obj = new Dictionary<string, object>();
            for (int i = 0; i < count; i++)
            {
                obj[$"prop{i}"] = i;
            }
            return obj;
        }
    }
}
