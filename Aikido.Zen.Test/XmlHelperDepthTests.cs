using Aikido.Zen.Core.Models;
using System.Text;
using System.Xml;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    public class XmlHelperDepthTests
    {
        [TestCase(0)]
        [TestCase(XmlHelper.MaxDepth - 1)]
        [TestCase(XmlHelper.MaxDepth)]
        [TestCase(XmlHelper.MaxDepth + 1)]
        [TestCase(14000)]
        public void FlattenXml_InspectsOnlyElementsBelowDepthLimit(int depth)
        {
            var document = new XmlDocument();
            document.LoadXml(CreateNestedXml(depth));
            var result = new List<UserInput>();

            XmlHelper.FlattenXml(result, document.DocumentElement!, "body");

            if (depth < XmlHelper.MaxDepth)
            {
                var key = "body" + string.Concat(Enumerable.Repeat(".a", depth));
                Assert.That(result, Is.EquivalentTo(new List<UserInput> { new UserInput(key, "leaf") }));
            }
            else
            {
                Assert.That(result, Is.Empty);
            }
        }

        [TestCase(0)]
        [TestCase(XmlHelper.MaxDepth - 1)]
        [TestCase(XmlHelper.MaxDepth)]
        [TestCase(XmlHelper.MaxDepth + 1)]
        [TestCase(14000)]
        public void XmlToObject_TruncatesAtDepthLimit(int depth)
        {
            var document = new XmlDocument();
            document.LoadXml(CreateNestedXml(depth));

            var result = XmlHelper.XmlToObject(document.DocumentElement!);

            for (var i = 0; i < Math.Min(depth, XmlHelper.MaxDepth); i++)
            {
                result = ((Dictionary<string, object>)result)["a"];
            }
            if (depth < XmlHelper.MaxDepth)
            {
                Assert.That(result, Is.EqualTo("leaf"));
            }
            else
            {
                Assert.That(result, Is.TypeOf<Dictionary<string, object>>());
                Assert.That((Dictionary<string, object>)result, Is.Empty);
            }
        }

        [TestCase("application/xml", false)]
        [TestCase("text/xml", false)]
        [TestCase("application/vnd.example+xml", false)]
        [TestCase("application/xml", true)]
        public async Task ReadAndFlattenHttpDataAsync_DeepXmlPreservesShallowSiblingsAndResetsStream(string contentType, bool multipart)
        {
            var content = "<root>" + CreateNestedXml(14000) + "<safe>retained</safe></root>";
            if (multipart)
            {
                content = "--boundary\r\nContent-Disposition: form-data; name=\"xml\"\r\n" +
                    $"Content-Type: {contentType}\r\n\r\n{content}\r\n--boundary--\r\n";
                contentType = "multipart/form-data; boundary=boundary";
            }
            using var body = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var empty = new Dictionary<string, string>();

            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(empty, empty, empty, empty, body, contentType);

            var prefix = multipart ? "body.section.0" : "body";
            Assert.That(result.FlattenedData, Is.EquivalentTo(new List<UserInput>
            {
                new UserInput(prefix + ".safe", "retained")
            }));
            var parsedBody = (Dictionary<string, object>)(multipart
                ? ((Dictionary<string, object>)result.ParsedBody)["xml"]
                : result.ParsedBody);
            Assert.That(parsedBody["safe"], Is.EqualTo("retained"));
            var capped = parsedBody;
            for (var i = 0; i < XmlHelper.MaxDepth; i++)
            {
                capped = (Dictionary<string, object>)capped["a"];
            }
            Assert.Multiple(() =>
            {
                Assert.That(capped, Is.Empty);
                Assert.That(body.CanRead, Is.True);
                Assert.That(body.Position, Is.Zero);
            });
        }

        private static string CreateNestedXml(int depth)
        {
            var xml = new StringBuilder();
            for (var i = 0; i <= depth; i++)
            {
                xml.Append("<a>");
            }
            xml.Append(" leaf ");
            for (var i = 0; i <= depth; i++)
            {
                xml.Append("</a>");
            }
            return xml.ToString();
        }
    }
}
