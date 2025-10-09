using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using System.Collections.Generic;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class SchemaHelperTests
    {
        [Test]
        public void MergeDataSchemas_WithSimpleTypes_MergesCorrectly()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "string" }
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "number" }
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);
            Assert.That(result.Type, Is.EquivalentTo(new[] { "string", "number" }));
        }

        [Test]
        public void MergeDataSchemas_WithObjects_MergesProperties()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "object" },
                Properties = new Dictionary<string, DataSchema>
                {
                    { "prop1", new DataSchema { Type = new[] { "string" } } }
                }
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "object" },
                Properties = new Dictionary<string, DataSchema>
                {
                    { "prop2", new DataSchema { Type = new[] { "number" } } }
                }
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);

            Assert.That(result.Type[0], Is.EqualTo("object"));
            Assert.That(result.Properties!.Count, Is.EqualTo(2));
            Assert.That(result.Properties["prop1"].Type[0], Is.EqualTo("string"));
            Assert.That(result.Properties["prop2"].Type[0], Is.EqualTo("number"));
            Assert.That(result.Properties["prop2"].Optional, Is.True);
        }

        [Test]
        public void MergeDataSchemas_WithArrays_MergesItems()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "array" },
                Items = new DataSchema { Type = new[] { "string" } }
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "array" },
                Items = new DataSchema { Type = new[] { "number" } }
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);

            Assert.That(result.Type[0], Is.EqualTo("array"));
            Assert.That(result.Items!.Type, Is.EquivalentTo(new[] { "string", "number" }));
        }

        [Test]
        public void MergeDataSchemas_WithDifferentFormats_ClearsFormat()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "string" },
                Format = "email"
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "string" },
                Format = "uri"
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);
            Assert.That(result.Format, Is.Null);
        }

        [Test]
        public void MergeDataSchemas_WithSameFormat_KeepsFormat()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "string" },
                Format = "email"
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "string" },
                Format = "email"
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);
            Assert.That(result.Format, Is.EqualTo("email"));
        }

        [Test]
        public void MergeDataSchemas_WithNestedObjects_MergesRecursively()
        {
            var schema1 = new DataSchema
            {
                Type = new[] { "object" },
                Properties = new Dictionary<string, DataSchema>
                {
                    {
                        "user", new DataSchema
                        {
                            Type = new[] { "object" },
                            Properties = new Dictionary<string, DataSchema>
                            {
                                { "name", new DataSchema { Type = new[] { "string" } } }
                            }
                        }
                    }
                }
            };

            var schema2 = new DataSchema
            {
                Type = new[] { "object" },
                Properties = new Dictionary<string, DataSchema>
                {
                    {
                        "user", new DataSchema
                        {
                            Type = new[] { "object" },
                            Properties = new Dictionary<string, DataSchema>
                            {
                                { "age", new DataSchema { Type = new[] { "number" } } }
                            }
                        }
                    }
                }
            };

            var result = SchemaHelper.MergeDataSchemas(schema1, schema2);

            Assert.That(result.Type[0], Is.EqualTo("object"));
            Assert.That(result.Properties!["user"].Type[0], Is.EqualTo("object"));
            Assert.That(result.Properties["user"].Properties!.Count, Is.EqualTo(2));
            Assert.That(result.Properties["user"].Properties["name"].Type[0], Is.EqualTo("string"));
            Assert.That(result.Properties["user"].Properties["age"].Type[0], Is.EqualTo("number"));
            Assert.That(result.Properties["user"].Properties["age"].Optional, Is.True);
        }

        [Test]
        public void MergeDataSchemas_WithNullSchema_ReturnsOtherSchema()
        {
            var schema = new DataSchema
            {
                Type = new[] { "string" }
            };

            var result1 = SchemaHelper.MergeDataSchemas(schema, null);
            var result2 = SchemaHelper.MergeDataSchemas(null, schema);

            Assert.That(result1.Type[0], Is.EqualTo("string"));
            Assert.That(result2.Type[0], Is.EqualTo("string"));
        }

        [Test]
        public void MergeDataSchemas_WithBothNull_ReturnsNull()
        {
            var result = SchemaHelper.MergeDataSchemas(null, null);
            Assert.That(result, Is.Null);
        }

        [Test]
        [Timeout(1000)] // Should complete in under 1 second
        public void GetDataSchema_WithCircularReference_DoesNotHangOrStackOverflow()
        {
            // Create a complex circular reference structure
            var parent = new Dictionary<string, object>();
            var child1 = new Dictionary<string, object>();
            var child2 = new Dictionary<string, object>();
            var grandchild = new Dictionary<string, object>();
            var list = new List<object>();

            // Create circular references at multiple levels
            parent["child1"] = child1;
            parent["child2"] = child2;
            parent["items"] = list;
            parent["name"] = "parent";

            child1["grandchild"] = grandchild;
            child1["parent"] = parent; // Back reference to parent
            child1["name"] = "child1";

            child2["sibling"] = child1;
            child2["self"] = child2; // Self reference
            child2["name"] = "child2";

            grandchild["root"] = parent; // Reference to root
            grandchild["parent"] = child1; // Reference to parent
            grandchild["self"] = grandchild; // Self reference
            grandchild["name"] = "grandchild";

            list.Add(parent); // Circular reference in array
            list.Add(child1);
            list.Add("string value");

            // This should complete without hanging or stack overflow
            var result = SchemaHelper.GetDataSchema(parent);

            Assert.That(result.Type[0], Is.EqualTo("object"));
            Assert.That(result.Properties, Is.Not.Null);
            Assert.That(result.Properties.ContainsKey("name"), Is.True);
            Assert.That(result.Properties.ContainsKey("child1"), Is.True);
            Assert.That(result.Properties.ContainsKey("child2"), Is.True);
        }
    }
}
