using System;
using System.Collections.Generic;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class DetectedAttackWaveTests
    {
        [Test]
        public void Type_ReturnsExpectedValue()
        {
            var evt = new DetectedAttackWave();
            Assert.That(evt.Type, Is.EqualTo("detected_attack_wave"));
        }

        [Test]
        public void Create_PopulatesEvent()
        {
            var context = new Context
            {
                RemoteAddress = "127.0.0.1",
                UserAgent = "unit-test-agent",
                Source = "test-source",
                User = new User("user-id", "user-name"),
            };

            var samples = new List<SuspiciousRequest>
            {
                new SuspiciousRequest { Method = "GET", Url = "/wp-config.php" }
            };

            var evt = DetectedAttackWave.Create(context, samples);

            Assert.Multiple(() =>
            {
                Assert.That(evt.Request.IpAddress, Is.EqualTo(context.RemoteAddress));
                Assert.That(evt.Request.UserAgent, Is.EqualTo(context.UserAgent));
                Assert.That(evt.Request.Source, Is.EqualTo(context.Source));
                Assert.That(evt.Attack.User, Is.EqualTo(context.User));
                Assert.That(evt.Agent, Is.Not.Null);
                Assert.That(evt.Attack.Metadata["samples"], Is.EqualTo(JsonSerializer.Serialize(samples, ZenApi.JsonSerializerOptions)));

                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Assert.That(evt.Time, Is.InRange(now - 1000, now + 1000));
            });
        }

        [Test]
        public void Create_WithNullContext_Throws()
        {
            Assert.That(
                () => DetectedAttackWave.Create(null, Array.Empty<SuspiciousRequest>()),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
