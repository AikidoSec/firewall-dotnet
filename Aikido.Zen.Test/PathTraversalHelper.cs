using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Exceptions;
using NUnit.Framework;
using System;
using System.Web;
using Aikido.Zen.Core;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class PathTraversalHelperTests
    {
        private Context _context;
        private const string ModuleName = "TestModule";
        private const string Operation = "TestOperation";

        [SetUp]
        public void Setup()
        {
            _context = new Context {
            AttackDetected = false,
            ParsedUserInput = new System.Collections.Generic.Dictionary<string, string>(),
            Body = new MemoryStream()
            };
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Agent.NewInstance(Mocks.ZenApiMock.CreateMock().Object);
        }

        [Test]
        public void DetectPathTraversal_WithNullContext_ReturnsTrue()
        {
            // Arrange
            object[] args = new object[] { "test.txt" };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, null, Operation);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithUrlDecode_SkipsDetection()
        {
            // Arrange
            _context.ParsedUserInput.Add("test", "../test.txt");
            object[] args = new object[] { "../test.txt" };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, nameof(HttpUtility.UrlDecode));

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithSinglePath_DetectsTraversal()
        {
            // Arrange
            _context.ParsedUserInput.Add("test", "../test.txt");
            string path = "/var/www/test.txt";

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(path, _context, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithMultiplePaths_DetectsTraversal()
        {
            // Arrange
            _context.ParsedUserInput.Add("test", "../test.txt");
            string[] paths = new[] { "/var/www/test1.txt", "/var/www/test2.txt" };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(paths, _context, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithSafePath_ReturnsFalse()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");
            _context.ParsedUserInput.Add("test", "test.txt");
            string path = "/var/www/test.txt";

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(path, _context, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithTraversalInNonDryMode_ThrowsException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");
            _context.ParsedUserInput.Add("test", "../test.txt");
            object[] args = new object[] { "/var/www/test.txt" };

            // Act & Assert
            Assert.Throws<AikidoException>(() => 
                PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation));
        }

        [Test]
        public void DetectPathTraversal_WithStringArrayArg_DetectsTraversal()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "false");
            _context.ParsedUserInput.Add("test", "../test.txt");
            object[] args = new object[] { new string[] { "/var/www/test1.txt", "/var/www/test2.txt" } };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithNonStringArg_IgnoresArg()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");
            _context.ParsedUserInput.Add("test", "../test.txt");
            object[] args = new object[] { 42 };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }
    }
}
