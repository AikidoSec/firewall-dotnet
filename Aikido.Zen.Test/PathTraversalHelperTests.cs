using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Exceptions;
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

        [TestCase("../test.txt", true, Description = "Detects traversal in single path")]
        [TestCase("safe.txt", false, Description = "Passes safe single path")]
        public void DetectPathTraversal_WithSinglePath(string path, bool expectedAttack)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "false");
            _context.ParsedUserInput.Add("test", path);
            object[] args = new object[] { path };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);  // Always true as validation completed
            Assert.That(_context.AttackDetected, Is.EqualTo(expectedAttack));
        }

        [Test]
        public void DetectPathTraversal_WithTraversalInNonDryMode_ThrowsException()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", "true");
            _context.ParsedUserInput.Add("query", "../test.txt");
            object[] args = new object[] { "/var/www/../test.txt" };

            // Act & Assert
            Assert.Throws<AikidoException>(() => 
                PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation));
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

        [Test]
        public void DetectPathTraversal_WithEmptyArray_ReturnsFalse()
        {
            // Arrange
            object[] args = new object[] { new string[0] };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);  // Validation passes for empty array
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithNullArrayElement_SkipsNullElement()
        {
            // Arrange
            string[] paths = new string[] { null, "safe.txt" };
            object[] args = new object[] { paths };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithMixedArrayContent_DetectsTraversal()
        {
            // Arrange
            _context.ParsedUserInput.Add("query", "../");
            object[] args = new object[] { 
                "safe.txt",
                new string[] { "../safe1.txt", "../unsafe.txt" },
                42
            };

            // Act
            bool result = PathTraversalHelper.DetectPathTraversal(args, ModuleName, _context, Operation);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }
    }
}
