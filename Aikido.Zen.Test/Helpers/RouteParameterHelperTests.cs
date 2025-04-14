
using System.Security.Cryptography;
using System.Text;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class RouteParameterHelperTests
    {
        private const string Lower = "abcdefghijklmnopqrstuvwxyz";
        private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Numbers = "0123456789";
        private const string Specials = "!#$%^&*|;:<>";

        private string SecretFromCharset(int length, string charset)
        {
            var random = new Random();
            return new string(Enumerable.Range(0, length)
                .Select(_ => charset[random.Next(charset.Length)])
                .ToArray());
        }

        [Test]
        public void BuildRouteFromUrl_InvalidUrls_ReturnsNull()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl(""), Is.Null);
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("http"), Is.Null);
        }

        [Test]
        public void BuildRouteFromUrl_RootUrls_ReturnsRoot()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/"), Is.EqualTo("/"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("http://localhost/"), Is.EqualTo("/"));
        }

        [Test]
        public void BuildRouteFromUrl_Numbers_ReplacesWithNumberParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/3"), Is.EqualTo("/posts/:number"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("http://localhost/posts/3"), Is.EqualTo("/posts/:number"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("http://localhost/posts/3/"), Is.EqualTo("/posts/:number"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("http://localhost/posts/3/comments/10"), Is.EqualTo("/posts/:number/comments/:number"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/blog/2023/05/great-article"), Is.EqualTo("/blog/:number/:number/great-article"));
        }

        [Test]
        public void BuildRouteFromUrl_Dates_ReplacesWithDateParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/2023-05-01"), Is.EqualTo("/posts/:date"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/2023-05-01/"), Is.EqualTo("/posts/:date"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/2023-05-01/comments/2023-05-01"), Is.EqualTo("/posts/:date/comments/:date"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/01-05-2023"), Is.EqualTo("/posts/:date"));
        }

        [Test]
        public void BuildRouteFromUrl_CommaNumbers_IgnoresCommaNumbers()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/3,000"), Is.EqualTo("/posts/3,000"));
        }

        [Test]
        public void BuildRouteFromUrl_ApiVersionNumbers_IgnoresApiVersionNumbers()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/v1/posts/3"), Is.EqualTo("/v1/posts/:number"));
        }

        [Test]
        public void BuildRouteFromUrl_UUIDs_ReplacesWithUuidParam()
        {
            // Test various UUID versions
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/d9428888-122b-11e1-b85c-61cd3cbb3210"), Is.EqualTo("/posts/:uuid")); // v1
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/000003e8-2363-21ef-b200-325096b39f47"), Is.EqualTo("/posts/:uuid")); // v2
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/a981a0c2-68b1-35dc-bcfc-296e52ab01ec"), Is.EqualTo("/posts/:uuid")); // v3
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/109156be-c4fb-41ea-b1b4-efe1671c5836"), Is.EqualTo("/posts/:uuid")); // v4
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/90123e1c-7512-523e-bb28-76fab9f2f73d"), Is.EqualTo("/posts/:uuid")); // v5
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/1ef21d2f-1207-6660-8c4f-419efbd44d48"), Is.EqualTo("/posts/:uuid")); // v6
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/017f22e2-79b0-7cc3-98c4-dc0c0c07398f"), Is.EqualTo("/posts/:uuid")); // v7
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/0d8f23a0-697f-83ae-802e-48f3756dd581"), Is.EqualTo("/posts/:uuid")); // v8
        }

        [Test]
        public void BuildRouteFromUrl_InvalidUUIDs_IgnoresInvalidUUIDs()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/00000000-0000-1000-6000-000000000000"),
                Is.EqualTo("/posts/00000000-0000-1000-6000-000000000000"));
        }

        [Test]
        public void BuildRouteFromUrl_Strings_IgnoresStrings()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/abc"), Is.EqualTo("/posts/abc"));
        }

        [Test]
        public void BuildRouteFromUrl_EmailAddresses_ReplacesWithEmailParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/login/john.doe@acme.com"), Is.EqualTo("/login/:email"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/login/john.doe+alias@acme.com"), Is.EqualTo("/login/:email"));
        }

        [Test]
        public void BuildRouteFromUrl_IPAddresses_ReplacesWithIpParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/1.2.3.4"), Is.EqualTo("/block/:ip"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/2001:2:ffff:ffff:ffff:ffff:ffff:ffff"), Is.EqualTo("/block/:ip"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/64:ff9a::255.255.255.255"), Is.EqualTo("/block/:ip"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/100::"), Is.EqualTo("/block/:ip"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/fec0::"), Is.EqualTo("/block/:ip"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/block/227.202.96.196"), Is.EqualTo("/block/:ip"));
        }

        [Test]
        public void BuildRouteFromUrl_Hashes_ReplacesWithHashParam()
        {
            using (var md5 = MD5.Create())
            {
                var md5Hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes("test"))).Replace("-", "").ToLower();
                Assert.That(RouteParameterHelper.BuildRouteFromUrl($"/files/{md5Hash}"), Is.EqualTo("/files/:hash"));
            }

            using (var sha1 = SHA1.Create())
            {
                var sha1Hash = BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes("test"))).Replace("-", "").ToLower();
                Assert.That(RouteParameterHelper.BuildRouteFromUrl($"/files/{sha1Hash}"), Is.EqualTo("/files/:hash"));
            }

            using (var sha256 = SHA256.Create())
            {
                var sha256Hash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes("test"))).Replace("-", "").ToLower();
                Assert.That(RouteParameterHelper.BuildRouteFromUrl($"/files/{sha256Hash}"), Is.EqualTo("/files/:hash"));
            }

            using (var sha512 = SHA512.Create())
            {
                var sha512Hash = BitConverter.ToString(sha512.ComputeHash(Encoding.UTF8.GetBytes("test"))).Replace("-", "").ToLower();
                Assert.That(RouteParameterHelper.BuildRouteFromUrl($"/files/{sha512Hash}"), Is.EqualTo("/files/:hash"));
            }
        }

        [Test]
        public void BuildRouteFromUrl_Secrets_ReplacesWithSecretParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/confirm/CnJ4DunhYfv2db6T1FRfciRBHtlNKOYrjoz"),
                Is.EqualTo("/confirm/:secret"));
        }

        [Test]
        public void BuildRouteFromUrl_ObjectIDs_ReplacesWithObjectIdParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/66ec29159d00113616fc7184"),
                Is.EqualTo("/posts/:objectId"));
        }

        [Test]
        public void BuildRouteFromUrl_ULIDs_ReplacesWithUlidParam()
        {
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/01ARZ3NDEKTSV4RRFFQ69G5FAV"),
                Is.EqualTo("/posts/:ulid"));
            Assert.That(RouteParameterHelper.BuildRouteFromUrl("/posts/01arz3ndektsv4rrffq69g5fav"),
                Is.EqualTo("/posts/:ulid"));
        }

        [Test]
        public void BuildRouteFromUrl_StaticFiles_DoesNotDetectAsSecrets()
        {
            var files = new[]
            {
                "ClientRouter.astro_astro_type_script_index_0_lang.AWhPxJ6s.js",
                "index.BRaz9DSe.css",
                "icon.DbNf-ftQ_Z18kUbq.svg",
                "Layout.astro_astro_type_script_index_1_lang.DBtfcKk0.js",
                "nunito-latin-wght-normal.BaTF6Vo7.woff2"
            };

            foreach (var file in files)
            {
                Assert.That(RouteParameterHelper.BuildRouteFromUrl($"/assets/{file}"),
                    Is.EqualTo($"/assets/{file}"));
            }
        }

        [Test]
        public void LooksLikeASecret_EmptyString_ReturnsFalse()
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(""), Is.False);
        }

        [TestCase("c")]
        [TestCase("NR")]
        [TestCase("7t3")]
        [TestCase("4qEK")]
        [TestCase("KJr6s")]
        [TestCase("KXiW4a")]
        [TestCase("Fupm2Vi")]
        [TestCase("jiGmyGfg")]
        [TestCase("SJPLzVQ8t")]
        [TestCase("OmNf04j6mU")]
        public void LooksLikeASecret_ShortStrings_ReturnsFalse(string input)
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.False);
        }

        [TestCase("rsVEExrR2sVDONyeWwND")]
        [TestCase(":2fbg;:qf$BRBc<2AG8&")]
        public void LooksLikeASecret_LongStrings_ReturnsTrue(string input)
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.True);
        }

        [Test]
        public void LooksLikeASecret_VeryLongString_ReturnsTrue()
        {
            var input = "efDJHhzvkytpXoMkFUgag6shWJktYZ5QUrUCTfecFELpdvaoAT3tekI4ZhpzbqLt";
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.True);
        }

        [Test]
        public void LooksLikeASecret_ContainsWhitespace_ReturnsFalse()
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret("rsVEExrR2sVDONyeWwND "), Is.False);
        }

        [Test]
        public void LooksLikeASecret_SingleCharset_ReturnsFalse()
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(SecretFromCharset(10, Lower)), Is.False);
            Assert.That(RouteParameterHelper.LooksLikeASecret(SecretFromCharset(10, Upper)), Is.False);
            Assert.That(RouteParameterHelper.LooksLikeASecret(SecretFromCharset(10, Numbers)), Is.False);
            Assert.That(RouteParameterHelper.LooksLikeASecret(SecretFromCharset(10, Specials)), Is.False);
        }

        [TestCase("development")]
        [TestCase("programming")]
        [TestCase("applications")]
        [TestCase("implementation")]
        [TestCase("environment")]
        public void LooksLikeASecret_CommonUrlTerms_ReturnsFalse(string input)
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.False);
        }

        [Test]
        public void LooksLikeASecret_WordSeparators_ReturnsFalse()
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret("this-is-a-secret-1"), Is.False);
        }

        [TestCase("1234567890")]
        [TestCase("12345678901234567890")]
        public void LooksLikeASecret_OnlyNumbers_ReturnsFalse(string input)
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.False);
        }

        [TestCase("yqHYTS<agpi^aa1")]
        [TestCase("hIofuWBifkJI5iVsSNKKKDpBfmMqJJwuXMxau6AS8WZaHVLDAMeJXo3BwsFyrIIm")]
        [TestCase("AG7DrGi3pDDIUU1PrEsj")]
        [TestCase("CnJ4DunhYfv2db6T1FRfciRBHtlNKOYrjoz")]
        [TestCase("Gic*EfMq:^MQ|ZcmX:yW1")]
        public void LooksLikeASecret_KnownSecrets_ReturnsTrue(string input)
        {
            Assert.That(RouteParameterHelper.LooksLikeASecret(input), Is.True);
        }

        [TestCase("/:uuid", true)]
        [TestCase("/:ulid", true)]
        [TestCase("/:objectId", true)]
        [TestCase("/:email", true)]
        [TestCase("/:hash", true)]
        [TestCase("/:secret", true)]
        [TestCase("/:number", true)]
        [TestCase("/:date", true)]
        [TestCase("/{param}", true)]
        [TestCase("/{productId}", true)]
        [TestCase("/api/:uuid", true)]
        [TestCase("/api/v1/:uuid", true)]
        [TestCase("/api/v2/:number", true)]
        [TestCase("/api/v3/{id}", true)]
        [TestCase("/users/:number", false)]
        [TestCase("/api/v3/users/{id}/roles", false)]
        [TestCase("/posts/:uuid/comments", false)] 
        [TestCase("/api/v1/users/:number", false)] 
        [TestCase("/api/v1/items/:id/details", false)]
        [TestCase("/users/123", false)]
        [TestCase("/products/some-product", false)]
        [TestCase("/files/document.pdf", false)]
        [TestCase("/", false)] // Root path
        [TestCase("", false)] // Empty path
        [TestCase("/path/with/multiple/segments", false)] // Multiple non-parameter segments
        public void PathIsSingleRouteParameter_VariousPaths_ReturnsExpectedResult(string path, bool expectedResult)
        {
            Assert.That(RouteParameterHelper.PathIsSingleRouteParameter(path), Is.EqualTo(expectedResult));
        }
    }
}
