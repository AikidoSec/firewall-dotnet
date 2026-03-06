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
            Assert.That(id, Is.EqualTo(user.Id));
            Assert.That(name, Is.EqualTo(user.Name));
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsNull()
        {
            // Arrange
            string id = null;
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.That("User ID cannot be null or empty", Is.EqualTo(ex.Message));
        }

        [Test]
        public void UserConstructor_ShouldAllowNullName()
        {
            // Arrange
            var id = "123";
            string name = null;

            // Act
            var user = new User(id, name);

            // Assert
            Assert.That(user.Name, Is.Null);
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsEmpty()
        {
            // Arrange
            var id = "";
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.That(ex.Message, Is.EqualTo("User ID cannot be null or empty"));
        }

        [Test]
        public void UserConstructor_ShouldThrowException_WhenIdIsWhitespace()
        {
            // Arrange
            var id = "   ";
            var name = "John Doe";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new User(id, name));
            Assert.That(ex.Message, Is.EqualTo("User ID cannot be null or empty"));
        }

    }
}
