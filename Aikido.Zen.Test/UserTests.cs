using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;

namespace Aikido.Zen.Test
{
    public class UserTests
    {
        [Test]
        public void UserConstructor_ShouldInitializeProperties()
        {
            // Arrange
            var id = "123";
            var name = "John Doe";

            // Act
            var user = new User(id, name);

            // Assert
            Assert.AreEqual(id, user.Id);
            Assert.AreEqual(name, user.Name);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsNull()
        {
            // Arrange
            string id = null;
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenNameIsNull()
        {
            // Arrange
            var id = "123";
            string name = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsEmpty()
        {
            // Arrange
            var id = "";
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenNameIsEmpty()
        {
            // Arrange
            var id = "123";
            var name = "";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsWhitespace()
        {
            // Arrange
            var id = "   ";
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenNameIsWhitespace()
        {
            // Arrange
            var id = "123";
            var name = "   ";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.AreEqual("User ID or name cannot be null or empty", ex.Message);
        }
    }
}
