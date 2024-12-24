using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;

namespace Aikido.Zen.Test
{
    public class AikidoExceptionTests
    {
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
            string ipAddress = "192.168.1.1";
            string expectedMessage = $"Request blocked from {ipAddress} to {route}";

            // Act
            var exception = AikidoException.RequestBlocked(route, ipAddress);

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
    }
}
