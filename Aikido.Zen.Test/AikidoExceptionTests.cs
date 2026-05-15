using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
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
        public void Blocked_WithSqlInjection_ShouldReturnCorrectMessage()
        {
            // Arrange
            string expectedMessage = $"SQL injection detected during operation: {SQLDialect.MicrosoftSQL.ToHumanName()}";

            // Act
            var exception = AikidoException.Blocked(
                AttackKind.SqlInjection,
                "operation",
                SQLDialect.MicrosoftSQL.ToHumanName());

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Blocked_WithShellInjection_ShouldReturnCorrectMessage()
        {
            // Arrange
            string expectedMessage = "Shell injection detected during operation";

            // Act
            var exception = AikidoException.Blocked(AttackKind.ShellInjection, "operation");

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Blocked_WithPathTraversal_ShouldReturnCorrectMessage()
        {
            var exception = AikidoException.Blocked(
                AttackKind.PathTraversal,
                "File.ReadAllBytes",
                "../etc/passwd");

            Assert.That(exception.Message, Is.EqualTo("Path traversal detected during File.ReadAllBytes: ../etc/passwd"));
        }

        [Test]
        public void Blocked_WithOutboundConnectionBlocked_ShouldReturnCorrectMessage()
        {
            // Arrange
            string hostname = "blocked.example";
            string expectedMessage = $"Zen has blocked an outbound connection during operation: {hostname}";

            // Act
            var exception = AikidoException.Blocked(
                AttackKind.OutboundConnectionBlocked,
                "operation",
                hostname);

            // Assert
            Assert.That(exception.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void Exception_ShouldPreserveStackTrace()
        {
            // Arrange & Act
            AikidoException? exception = null;
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
                    It.IsAny<Exception?>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
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
