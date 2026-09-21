using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class CustomEventTests
    {
        [Test]
        public void Create_BuildsTheRealtimePayload()
        {
            var context = new Context
            {
                Method = "POST",
                RemoteAddress = "192.0.2.1",
                UserAgent = "test-agent",
                Url = "https://example.com/login?token=secret&email=user@example.com",
                Source = "aspnetcore",
                Route = "/login",
                User = new User("user-1", "Jane Doe"),
            };

            var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var customEvent = CustomEvent.Create("user.login_failed", context);
            var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.Multiple(() =>
            {
                Assert.That(customEvent.Type, Is.EqualTo("custom"));
                Assert.That(customEvent.Name, Is.EqualTo("user.login_failed"));
                Assert.That(customEvent.Request.Method, Is.EqualTo("POST"));
                Assert.That(customEvent.Request.IpAddress, Is.EqualTo("192.0.2.1"));
                Assert.That(customEvent.Request.UserAgent, Is.EqualTo("test-agent"));
                Assert.That(customEvent.Request.Source, Is.EqualTo("aspnetcore"));
                Assert.That(customEvent.Request.Route, Is.EqualTo("/login"));
                Assert.That(customEvent.User, Is.SameAs(context.User));
                Assert.That(customEvent.Agent, Is.Not.Null);
                Assert.That(customEvent.Time, Is.InRange(before, after));
            });

            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(customEvent, ZenApi.JsonSerializerOptions));
            var request = payload.RootElement.GetProperty("request");
            Assert.Multiple(() =>
            {
                Assert.That(request.TryGetProperty("headers", out _), Is.False);
                Assert.That(request.TryGetProperty("body", out _), Is.False);
                Assert.That(request.TryGetProperty("url", out _), Is.False);
            });
        }

        [Test]
        public void Create_WithoutContext_Throws()
        {
            Assert.That(
                () => CustomEvent.Create("user.login_failed", null),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
