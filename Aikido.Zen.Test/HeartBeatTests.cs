using Aikido.Zen.Core.Models.Events;
using NUnit;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test
{
    public class HeartbeatTests
    {
        private Heartbeat _heartbeat;

        [SetUp]
        public void Setup()
        {
            _heartbeat = new Heartbeat();
        }

        [Test]
        public void Constructor_CreatesValidInstance()
        {
            // Assert
            Assert.That(_heartbeat, Is.Not.Null);
            Assert.That(_heartbeat.Type, Is.EqualTo("heartbeat"));
            Assert.That(_heartbeat.Stats, Is.Not.Null);
        }

        [Test]
        public void Properties_SetAndGetCorrectly()
        {
            // Arrange
            var stats = new Stats();
            var hostnames = new List<Host> { new Host() };
            var routes = new List<Route> { new Route() };
            var users = new List<UserExtended> { new UserExtended("123", "username") };
            var agentInfo = new AgentInfo();

            // Act
            _heartbeat.Stats = stats;
            _heartbeat.Hostnames = hostnames;
            _heartbeat.Routes = routes;
            _heartbeat.Users = users;
            _heartbeat.Agent = agentInfo;

            // Assert
            Assert.That(_heartbeat.Stats, Is.EqualTo(stats));
            Assert.That(_heartbeat.Hostnames, Is.EqualTo(hostnames));
            Assert.That(_heartbeat.Routes, Is.EqualTo(routes));
            Assert.That(_heartbeat.Users, Is.EqualTo(users));
            Assert.That(_heartbeat.Agent, Is.EqualTo(agentInfo));
        }

        [Test]
        public void Time_ReturnsCurrentUTCTimestamp()
        {
            // Arrange
            var beforeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Act
            var heartbeatTime = _heartbeat.Time;
            
            var afterTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Assert
            Assert.That(heartbeatTime, Is.GreaterThanOrEqualTo(beforeTime));
            Assert.That(heartbeatTime, Is.LessThanOrEqualTo(afterTime));
        }

        [Test]
        public void ScheduleId_HasCorrectValue()
        {
            Assert.That(Heartbeat.ScheduleId, Is.EqualTo("heartbeat"));
        }

        [Test]
        public void Interval_HasCorrectValue()
        {
#if DEBUG
            Assert.That(Heartbeat.Interval, Is.EqualTo(TimeSpan.FromMinutes(1)));
#else
            Assert.That(Heartbeat.Interval, Is.EqualTo(TimeSpan.FromMinutes(10)));
#endif
        }

        [Test]
        public void Properties_WithNullCollections_HandledGracefully()
        {
            // Act
            _heartbeat.Hostnames = null;
            _heartbeat.Routes = null;
            _heartbeat.Users = null;

            // Assert
            Assert.That(_heartbeat.Hostnames, Is.Null);
            Assert.That(_heartbeat.Routes, Is.Null);
            Assert.That(_heartbeat.Users, Is.Null);
        }
    }
}
