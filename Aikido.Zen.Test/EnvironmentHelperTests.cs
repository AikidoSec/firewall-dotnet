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
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(dryMode);
        }

        [Test]
        public void DryMode_ShouldReturnFalse_WhenEnvironmentVariableIsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");

            // Act
            var dryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(dryMode, Is.False);
        }

        [Test]
        public void AikidoUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_ENDPOINT", "https://custom.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://custom.aikido.dev"));
        }

        [Test]
        public void AikidoUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_ENDPOINT", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);

            // Act
            var url = EnvironmentHelper.AikidoUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://guard.aikido.dev"));
        }

        [TestCase("AIK_RUNTIME_1_2_US_random", "https://guard.us.aikido.dev")]
        [TestCase("AIK_RUNTIME_1_2_ME_random", "https://guard.me.aikido.dev")]
        [TestCase("AIK_RUNTIME_1_2_AU_random", "https://guard.au.aikido.dev")]
        [TestCase("AIK_RUNTIME_1_2_EU_random", "https://guard.aikido.dev")]
        [TestCase("AIK_RUNTIME_1_2_random", "https://guard.aikido.dev")] // old format without region
        [TestCase(null, "https://guard.aikido.dev")]
        [TestCase("not_a_runtime_token", "https://guard.aikido.dev")]
        public void AikidoUrl_ShouldBeDerivedFromTokenRegion_WhenEndpointIsNotSet(string? token, string expectedUrl)
        {
            // Arrange
            var originalEndpoint = Environment.GetEnvironmentVariable("AIKIDO_ENDPOINT");
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_ENDPOINT", null);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", token);

                // Act
                var url = EnvironmentHelper.AikidoUrl;

                // Assert
                Assert.That(url, Is.EqualTo(expectedUrl));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_ENDPOINT", originalEndpoint);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
            }
        }

        [TestCase("AIK_RUNTIME_1_2_US_random", "US")]
        [TestCase("AIK_RUNTIME_1_2_ME_random", "ME")]
        [TestCase("AIK_RUNTIME_1_2_AU_random", "AU")]
        [TestCase("AIK_RUNTIME_1_2_random", "EU")] // old format without region
        [TestCase(null, "EU")]
        [TestCase("", "EU")]
        [TestCase("not_a_runtime_token", "EU")]
        public void ExtractRegionFromToken_ShouldReturnExpectedRegion(string? token, string expectedRegion)
        {
            // Act
            var region = EnvironmentHelper.ExtractRegionFromToken(token);

            // Assert
            Assert.That(region, Is.EqualTo(expectedRegion));
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnExpectedValue_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_ENDPOINT", "https://custom-realtime.aikido.dev");

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://custom-realtime.aikido.dev"));
        }

        [Test]
        public void AikidoRealtimeUrl_ShouldReturnDefaultValue_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_ENDPOINT", null);

            // Act
            var url = EnvironmentHelper.AikidoRealtimeUrl;

            // Assert
            Assert.That(url, Is.EqualTo("https://runtime.aikido.dev"));
        }

        [Test]
        public void BlockInvalidSql_ShouldReturnFalse_WhenEnvironmentVariableIsNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL", null);

            // Act
            var blockInvalidSql = EnvironmentHelper.BlockInvalidSql;

            // Assert
            Assert.That(blockInvalidSql, Is.False);
        }

        [TestCase(null, false)]
        [TestCase("false", false)]
        [TestCase("true", true)]
        [TestCase("1", true)]
        public void DisableEndpointRoutingCheck_ShouldReturnExpectedValue(string? value, bool expected)
        {
            var originalValue = Environment.GetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK");

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", value);

                var disableEndpointRoutingCheck = EnvironmentHelper.DisableEndpointRoutingCheck;

                Assert.That(disableEndpointRoutingCheck, Is.EqualTo(expected));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", originalValue);
            }
        }
    }
}
