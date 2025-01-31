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
        [Test]
        public void DebugLog_ShouldLogMessage_WhenIsDebuggingIsTrue()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
            var message = "Test debug message";

            // Act
            LogHelper.DebugLog(loggerMock.Object, message);

            // Assert
            loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void DebugLog_ShouldNotLogMessage_WhenIsDebuggingIsFalse()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "false");
            var message = "Test debug message";

            // Act
            LogHelper.DebugLog(loggerMock.Object, message);

            // Assert
            loggerMock.Verify(logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
        }
    }
}
