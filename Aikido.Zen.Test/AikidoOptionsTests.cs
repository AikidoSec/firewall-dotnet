using Aikido.Zen.Core;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class AikidoOptionsTests
    {
        private AikidoOptions _options;

        [SetUp]
        public void Setup()
        {
            _options = new AikidoOptions();
        }

        [Test]
        public void SectionName_HasCorrectValue()
        {
            Assert.That(AikidoOptions.SectionName, Is.EqualTo("Aikido"));
        }

        [Test]
        public void AikidoToken_CanBeSetAndRetrieved()
        {
            // Arrange
            var token = "test-token-123";

            // Act
            _options.AikidoToken = token;

            // Assert
            Assert.That(_options.AikidoToken, Is.EqualTo(token));
        }

        [Test]
        public void AikidoUrl_CanBeSetAndRetrieved()
        {
            // Arrange
            var url = "https://test.aikido.com";

            // Act
            _options.AikidoUrl = url;

            // Assert
            Assert.That(_options.AikidoUrl, Is.EqualTo(url));
        }

        [Test]
        public void Properties_InitiallyNull()
        {
            // Assert
            Assert.That(_options.AikidoToken, Is.Null);
            Assert.That(_options.AikidoUrl, Is.Null);
        }
    }
}
