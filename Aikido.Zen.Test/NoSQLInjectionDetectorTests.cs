using System.Text.Json;
using NUnit.Framework;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test
{
    public class NoSQLInjectionDetectorTests
    {
        [Test]
        public void DetectNoSQLInjection_EmptyFilterAndRequest_ShouldReturnFalse()
        {
            // Arrange
            var context = new Context();
            var filter = JsonDocument.Parse("{}").RootElement;

            // Act
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void DetectNoSQLInjection_NonObjectFilter_ShouldReturnFalse()
        {
            // Arrange
            var context = new Context();
            var filter = JsonDocument.Parse("\"abc\"").RootElement;

            // Act
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void DetectNoSQLInjection_UsingGtInQueryParameter_ShouldReturnTrue()
        {
            // Arrange
            var context = new Context
            {
                Query = JsonDocument.Parse("{ \"title\": { \"$gt\": \"\" } }").RootElement
            };
            var filter = JsonDocument.Parse("{ \"title\": { \"$gt\": \"\" } }").RootElement;

            // Act
            var result = NoSQLInjectionDetector.DetectNoSQLInjection(context, filter);

            // Assert
            Assert.IsTrue(result);
        }

        // Additional tests can be added here following the same pattern
    }
}
