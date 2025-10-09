using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Tests.Mocks;

namespace Aikido.Zen.Test.Helpers
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
            _context = new Context
            {
                AttackDetected = false,
                ParsedUserInput = new System.Collections.Generic.Dictionary<string, string>(),
                Body = new MemoryStream()
            };
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
        }

        [Test]
        public void DetectPathTraversal_WithNullContext_ReturnsFalse()
        {
            // Arrange
            string[] args = new string[] { "test.txt" };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, null, Operation);

            // Assert
            Assert.That(result, Is.False);
        }

        [TestCase("../test.txt", true, Description = "Detects traversal in single path")]
        [TestCase("safe.txt", false, Description = "Passes safe single path")]
        public void DetectPathTraversal_WithSinglePath(string path, bool expectedAttack)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _context.ParsedUserInput.Add("test", path);
            string[] args = new string[] { path };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.EqualTo(expectedAttack));
            Assert.That(_context.AttackDetected, Is.EqualTo(expectedAttack));
        }

        [Test]
        public void DetectPathTraversal_WithEmptyArray_ReturnsFalse()
        {
            // Arrange
            string[] args = new string[0];

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithNullArrayElement_SkipsNullElement()
        {
            // Arrange
            string[] paths = new string[] { null, "safe.txt" };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(paths, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }
    }
}
