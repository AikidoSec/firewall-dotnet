using System.Text.Json;
using NUnit.Framework;
using Aikido.Zen.Core.Vulnerabilities;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Test class for NoSQLInjectionDetector.
    /// </summary>
    public class NoSQLInjectionDetectorTests
    {
        private Context CreateContext(
            Dictionary<string, string>? query = null,
            string? body = null,
            string? contentType = null,
            Dictionary<string, string>? headers = null,
            Dictionary<string, string>? cookies = null,
            Dictionary<string, string>? routeParams = null)
        {

            contentType ??= "application/json";
            var queryDictionary = new Dictionary<string, string[]>(query?.ToDictionary(q => q.Key, q => new[] { q.Value }) ?? new Dictionary<string, string[]>());
            var headersDictionary = new Dictionary<string, string[]>(headers?.ToDictionary(h => h.Key, h => new[] { h.Value }) ?? new Dictionary<string, string[]>());
            var cookiesDictionary = cookies?.ToDictionary(c => c.Key, c => c.Value) ?? new Dictionary<string, string>();

            var context = new Context
            {
                Query = queryDictionary,
                Headers = headersDictionary,
                Cookies = cookiesDictionary,
            };

            var httpData = HttpHelper.ReadAndFlattenHttpDataAsync(
                   queryParams: queryDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                   headers: headersDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                   cookies: context.Cookies,
                   body: new MemoryStream(body != null ? System.Text.Encoding.UTF8.GetBytes(body) : new byte[0]),
                   contentType: contentType,
                   contentLength: body?.Length ?? 0
               ).Result;

            context.ParsedUserInput = httpData.FlattenedData;

            return context;
        }

        [Test]
        public void DetectNoSQLInjection_EmptyFilterAndRequest_ShouldReturnFalse()
        {
            var context = CreateContext();
            var filter = JsonDocument.Parse("{}").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectNoSQLInjection_NonObjectFilter_ShouldReturnFalse()
        {
            var context = CreateContext();
            var filter = JsonDocument.Parse("\"abc\"").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectNoSQLInjection_UsingGtInQueryParameter_ShouldReturnTrue()
        {
            var context = CreateContext(new Dictionary<string, string> { { "title", "{ \"$gt\": \"\" }" } });
            var filter = JsonDocument.Parse("{ \"title\": { \"$gt\": \"\" } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_SafeFilter_ShouldReturnFalse()
        {
            var context = CreateContext(new Dictionary<string, string> { { "title", "title" } });
            var filter = JsonDocument.Parse("{ \"$and\": [{ \"title\": \"title\" }] }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInBody_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"title\": { \"$ne\": null } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInBodyDifferentName_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"myTitle\": { \"$ne\": null } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInsideAnd_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"$and\": [{ \"title\": { \"$ne\": null } }, { \"published\": true }] }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInsideOr_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"$or\": [{ \"title\": { \"$ne\": null } }, { \"published\": true }] }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInsideNor_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"$nor\": [{ \"title\": { \"$ne\": null } }, { \"published\": true }] }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInsideNot_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"title\": { \"$ne\": null } }");
            var filter = JsonDocument.Parse("{ \"$not\": { \"title\": { \"$ne\": null } } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeNestedInBody_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"nested\": { \"nested\": { \"$ne\": null } } }");
            var filter = JsonDocument.Parse("{ \"$not\": { \"title\": { \"$ne\": null } } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingNeInJWTInHeaders_ShouldReturnTrue()
        {
            var context = CreateContext(headers: new Dictionary<string, string> { { "Authorization", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwidXNlcm5hbWUiOnsiJG5lIjpudWxsfSwiaWF0IjoxNTE2MjM5MDIyfQ._jhGJw9WzB6gHKPSozTFHDo9NOHs3CNOlvJ8rWy6VrQ" } });
            var filter = JsonDocument.Parse("{ \"username\": { \"$ne\": null } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingGtAndLtInQueryParameter_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"age\": { \"$gt\": \"21\", \"$lt\": \"100\" } }");
            var filter = JsonDocument.Parse("{ \"age\": { \"$gt\": \"21\", \"$lt\": \"100\" } }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_UsingWhereJsInjectSleep_ShouldReturnTrue()
        {
            var context = CreateContext(body: "{ \"name\": \"a' && sleep(2000) && 'b\" }");
            var filter = JsonDocument.Parse("{ \"$where\": \"this.name === 'a' && sleep(2000) && 'b'\" }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectNoSQLInjection_DoesNotDetectIfNotAStringJsInjection_ShouldReturnFalse()
        {
            var context = CreateContext(body: "{ \"test\": 123 }");
            var filter = JsonDocument.Parse("{ \"$where\": \"this.name === 123\" }").RootElement;
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);
            Assert.That(result, Is.False);
        }
    }
}
