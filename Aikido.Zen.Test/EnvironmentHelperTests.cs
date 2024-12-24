using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test
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
            Assert.AreEqual("test_token", token);
        }

        [Test]
        public void DryMode_ShouldReturnTrue_WhenEnvironmentVariableIsNotTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "false");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.IsTrue(dryMode);
        }

        [Test]
        public void DryMode_ShouldReturnFalse_WhenEnvironmentVariableIsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.IsFalse(dryMode);
        }

        [Test]
        public void AikidoUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_URL", "https://custom.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.AreEqual("https://custom.aikido.dev", url);
        }

        [Test]
        public void AikidoUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_URL", null);

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.AreEqual("https://guard.aikido.dev", url);
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "https://custom-realtime.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.AreEqual("https://custom-realtime.aikido.dev", url);
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", null);

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.AreEqual("https://runtime.aikido.dev", url);
        }
    }
}
