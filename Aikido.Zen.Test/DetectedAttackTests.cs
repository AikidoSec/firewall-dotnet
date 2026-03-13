using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace Aikido.Zen.Test
{
    public class DetectedAttackTests
    {
        [Test]
        public void Type_ReturnsCorrectValue()
        {
            // Arrange
            var attack = new DetectedAttack();

            // Act
            var result = attack.Type;

            // Assert
            Assert.That(result, Is.EqualTo("detected_attack"));
        }

        [Test]
        public void Time_ReturnsCurrentUnixTimestamp()
        {
            // Arrange
            var attack = new DetectedAttack();

            // Act
            var result = attack.Time;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Assert
            // Allow 1 second difference to account for test execution time
            Assert.That(result, Is.InRange(now - 1000, now + 1000));
        }

        [Test]
        public void Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var request = new RequestInfo
            {
                Method = "POST",
                Url = "http://example.com/test",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };

            var attackInfo = new Attack
            {
                Kind = "sql_injection",
                Source = "query",
                Payload = "SELECT * FROM users",
                Operation = "read",
                Module = "authentication",
                Blocked = true,
                Metadata = new Dictionary<string, object>
                {
                    { "severity", "high" }
                }
            };

            var agentInfo = new AgentInfo
            {
                Hostname = "test-host",
                Version = "1.0.0"
            };

            // Act
            var detectedAttack = new DetectedAttack
            {
                Request = request,
                Attack = attackInfo,
                Agent = agentInfo
            };

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(detectedAttack.Request, Is.EqualTo(request));
                Assert.That(detectedAttack.Attack, Is.EqualTo(attackInfo));
                Assert.That(detectedAttack.Agent, Is.EqualTo(agentInfo));
            });
        }

        [Test]
        public void Properties_DefaultToNull()
        {
            // Arrange & Act
            var detectedAttack = new DetectedAttack();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(detectedAttack.Request, Is.Null);
                Assert.That(detectedAttack.Attack, Is.Null);
                Assert.That(detectedAttack.Agent, Is.Null);
            });
        }

        [Test]
        public void Create_WithPaths_UsesFirstPath()
        {
            var context = new Context
            {
                Url = "http://example.com/request",
                Method = "GET",
                Headers = new Dictionary<string, string>(),
                Body = new MemoryStream()
            };

            var detectedAttack = DetectedAttack.Create(
                AttackKind.Ssrf,
                Source.Query,
                "http://127.0.0.1/admin",
                "HttpClient.SendAsync",
                context,
                "System.Net.Http",
                new Dictionary<string, object> { { "hostname", "127.0.0.1" } },
                true,
                new[] { ".url", ".backup" });

            Assert.That(detectedAttack.Attack.Path, Is.EqualTo(".url"));
        }

        [Test]
        public void Create_WithStoredSsrf_DoesNotRequireContextOrSource()
        {
            var detectedAttack = DetectedAttack.Create(
                AttackKind.StoredSsrf,
                null,
                null,
                "HttpClient.SendAsync",
                null,
                "System.Net.Http",
                new Dictionary<string, object>
                {
                    { "hostname", "imds.test.com" },
                    { "privateIP", "169.254.169.254" }
                },
                true,
                Array.Empty<string>());

            Assert.Multiple(() =>
            {
                Assert.That(detectedAttack.Attack.Kind, Is.EqualTo("stored_ssrf"));
                Assert.That(detectedAttack.Attack.Source, Is.Null);
                Assert.That(detectedAttack.Request, Is.Null);
            });
        }
    }
}
