using System;
using System.Collections.Generic;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Test.Helpers
{
    /// <summary>
    /// Unit tests for the LogHelper class.
    /// </summary>
    public class LogHelperTests
    {
        private Mock<ILogger> _loggerMock;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger>();
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "false");

            LogHelper.ClearQueue();
        }

        [Test]
        public void DebugLog_ShouldLogMessage_WhenIsDebuggingIsTrue()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
            var message = "Test debug message";

            LogHelper.DebugLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().EndsWith(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void DebugLog_ShouldNotLogMessage_WhenIsDebuggingIsFalse()
        {
            var message = "Test debug message";

            LogHelper.DebugLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
        }

        [Test]
        public void DebugLog_ShouldSanitizeMessage_WhenMessageContainsDangerousCharacters()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
            var message = "Test\nmessage\rwith\tdangerous\ncharacters";
            var expectedSanitizedContent = "Testmessagewithdangerouscharacters";

            LogHelper.DebugLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedSanitizedContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void DebugLog_ShouldAddPrefix_WhenMessageDoesNotHaveIt()
        {
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
            var message = "Safe message";
            var expectedPrefix = "AIKIDO: ";

            LogHelper.DebugLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith(expectedPrefix) && v.ToString().EndsWith(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void AttackLog_ShouldLogMessageAsInformation()
        {
            var message = "Test attack message";

            LogHelper.AttackLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().EndsWith(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void AttackLog_ShouldSanitizeMessage_WhenMessageContainsDangerousCharacters()
        {
            var message = "Attack\nattempt\rwith\tdetails";
            var expectedSanitizedContent = "Attackattemptwithdetails";

            LogHelper.AttackLog(_loggerMock.Object, message);

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedSanitizedContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void AttackLog_RateLimiting_ShouldLogUpToMaxLogsAndThenSuppress()
        {
            int maxLogs = 1000;
            var message = "Rate limit test message";

            for (int i = 0; i < maxLogs; i++)
            {
                LogHelper.AttackLog(_loggerMock.Object, $"{message} {i + 1}");
            }
            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("AIKIDO: Rate limit test message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(maxLogs));

            LogHelper.AttackLog(_loggerMock.Object, $"{message} {maxLogs + 1}");

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("AIKIDO: Rate limit test message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(maxLogs));
        }
    }
}
