using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test.Helpers
{
    public class EnvironmentHelperTests
    {
        [Test]
        public void Token_ShouldReturnExpectedValue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test_token");

            // Act
            var token = EnvironmentHelper.Token;

            // Assert
            Assert.That(token, Is.EqualTo("test_token"));
        }

        [Test]
        public void DryMode_ShouldReturnTrue_WhenEnvironmentVariableIsNotTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "false");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(dryMode);
        }

        [Test]
        public void DryMode_ShouldReturnFalse_WhenEnvironmentVariableIsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(dryMode, Is.False);
        }

        [Test]
        public void AikidoUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_URL", "https://custom.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://custom.aikido.dev"));
        }

        [Test]
        public void AikidoUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_URL", null);

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://guard.aikido.dev"));
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "https://custom-realtime.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://custom-realtime.aikido.dev"));
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", null);

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://runtime.aikido.dev"));
        }
    }
}
