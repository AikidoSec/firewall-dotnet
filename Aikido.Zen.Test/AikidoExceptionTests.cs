using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;
using Microsoft.Extensions.Logging;
using Moq;

namespace Aikido.Zen.Test
{
    public class AikidoExceptionTests
    {
        private Mock<ILogger> _mockLogger;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [TearDown]
        public void TearDown()
        {
            // Reset to NullLogger after each test to avoid affecting other tests
            AikidoException.ConfigureLogger(null);
        }

        [Test]
        public void Constructor_WithMessage_ShouldSetMessage()
        {
            // Arrange
            string expectedMessage = "Test exception message";

            // Act
            var exception = new AikidoException(expectedMessage);

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception, Is.InstanceOf<Exception>());
        }

        [Test]
        public void Constructor_WithoutMessage_ShouldSetDefaultMessage()
        {
            // Arrange
            string expectedMessage = "Unknown threat blocked";

            // Act
            var exception = new AikidoException();

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception, Is.InstanceOf<Exception>());
        }

        [Test]
        public void SQLInjectionDetected_ShouldReturnCorrectMessage()
        {
            // Arrange
            string dialect = SQLDialect.MicrosoftSQL.ToHumanName();
            string expectedMessage = $"{SQLDialect.MicrosoftSQL.ToHumanName()}: SQL injection detected";

            // Act
            var exception = AikidoException.SQLInjectionDetected(dialect);

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void ShellInjectionDetected_ShouldReturnCorrectMessage()
        {
            // Arrange
            string expectedMessage = $"Shell injection detected";

            // Act
            var exception = AikidoException.ShellInjectionDetected();

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void RequestBlocked_ShouldReturnCorrectMessage()
        {
            // Arrange
            string route = "/api/data";
            string expectedMessage = $"Request blocked: {route}";

            // Act
            var exception = AikidoException.RequestBlocked(route);

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Ratelimited_ShouldReturnCorrectMessage()
        {
            // Arrange
            string route = "/api/data";
            string expectedMessage = $"Ratelimited: {route}";

            // Act
            var exception = AikidoException.RateLimited(route);

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Exception_ShouldPreserveStackTrace()
        {
            // Arrange & Act
            AikidoException exception = null;
            try
            {
                ThrowAikidoException();
            }
            catch (AikidoException ex)
            {
                exception = ex;
            }

            // Assert
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.StackTrace, Does.Contain(nameof(ThrowAikidoException)));
        }

        private void ThrowAikidoException()
        {
            throw new AikidoException("Test exception");
        }

        [Test]
        public void ConfigureLogger_WithNullLogger_ShouldUseNullLogger()
        {
            // Act
            AikidoException.ConfigureLogger(null);
            var exception = new AikidoException("Test message");

            // Assert - should not throw any exceptions
            Assert.That(exception.Message, Is.EqualTo("Test message"));
        }

        [Test]
        public void Constructor_WithLogger_ShouldLogError()
        {
            // Arrange
            AikidoException.ConfigureLogger(_mockLogger.Object);
            var message = "Test exception message";

            // Act
            var exception = new AikidoException(message);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [Test]
        public void Constructor_WithoutLogger_ShouldNotThrowException()
        {
            // Arrange
            AikidoException.ConfigureLogger(null);

            // Act & Assert
            Assert.DoesNotThrow(() => new AikidoException("Test message"));
        }
    }
}
