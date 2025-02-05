using System.Text.Json;
using NUnit.Framework;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    public class JsonHelperTests
    {
        [Test]
        public void TryParseJson_ValidJsonObject_ShouldReturnTrue()
        {
            // Arrange
            string jsonString = "{ \"key\": \"value\" }";

            // Act
            bool result = JsonHelper.TryParseJson(jsonString, out JsonElement jsonElement);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(jsonElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        }

        [Test]
        public void TryParseJson_ValidJsonArray_ShouldReturnTrue()
        {
            // Arrange
            string jsonString = "[1, 2, 3]";

            // Act
            bool result = JsonHelper.TryParseJson(jsonString, out JsonElement jsonElement);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(jsonElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        }

        [Test]
        public void TryParseJson_InvalidJson_ShouldReturnFalse()
        {
            // Arrange
            string jsonString = "not a json";

            // Act
            bool result = JsonHelper.TryParseJson(jsonString, out JsonElement jsonElement);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseJson_EmptyString_ShouldReturnFalse()
        {
            // Arrange
            string jsonString = "";

            // Act
            bool result = JsonHelper.TryParseJson(jsonString, out JsonElement jsonElement);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseJson_NullString_ShouldReturnFalse()
        {
            // Arrange
            string jsonString = null;

            // Act
            bool result = JsonHelper.TryParseJson(jsonString, out JsonElement jsonElement);

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
