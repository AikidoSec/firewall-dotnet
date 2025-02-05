using NUnit.Framework;
using Aikido.Zen.Core.Helpers;
using System.Text.Json;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Tests for the JwtHelper class.
    /// </summary>
    [TestFixture]
    public class JwtHelperTests
    {
        /// <summary>
        /// Test decoding a valid JWT.
        /// </summary>
        [Test]
        public void TryDecodeAsJwt_ValidJwt_ReturnsDecodedObject()
        {
            // Arrange
            var validJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

            // Act
            var (isJwt, decodedObject) = JwtHelper.TryDecodeAsJwt(validJwt);

            // Assert
            Assert.That(isJwt, Is.True);
            Assert.That(decodedObject, Is.Not.Null);
            Assert.That(decodedObject.ToString(), Does.Contain("John Doe"));
        }

        /// <summary>
        /// Test decoding an invalid JWT.
        /// </summary>
        [Test]
        public void TryDecodeAsJwt_InvalidJwt_ReturnsFalse()
        {
            // Arrange
            var invalidJwt = "invalid.jwt.token";

            // Act
            var (isJwt, decodedObject) = JwtHelper.TryDecodeAsJwt(invalidJwt);

            // Assert
            Assert.That(isJwt, Is.False);
            Assert.That(decodedObject, Is.Null);
        }

        /// <summary>
        /// Test decoding a non-JWT string.
        /// </summary>
        [Test]
        public void TryDecodeAsJwt_NonJwtString_ReturnsFalse()
        {
            // Arrange
            var nonJwt = "not.a.jwt";

            // Act
            var (isJwt, decodedObject) = JwtHelper.TryDecodeAsJwt(nonJwt);

            // Assert
            Assert.That(isJwt, Is.False);
            Assert.That(decodedObject, Is.Null);
        }
    }
}
