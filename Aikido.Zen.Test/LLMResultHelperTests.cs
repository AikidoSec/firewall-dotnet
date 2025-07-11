using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class LLMResultHelperTests
    {
        [Test]
        public void ResolveResult_WithNullInput_ReturnsNull()
        {
            // Act
            var result = LLMResultHelper.ResolveResult(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ResolveResult_WithRegularObject_ReturnsOriginalObject()
        {
            // Arrange
            var input = "test string";

            // Act
            var result = LLMResultHelper.ResolveResult(input);

            // Assert
            Assert.That(result, Is.EqualTo(input));
            Assert.That(result, Is.SameAs(input));
        }

        [Test]
        public void ResolveResult_WithComplexObject_ReturnsOriginalObject()
        {
            // Arrange
            var input = new { Name = "Test", Value = 42 };

            // Act
            var result = LLMResultHelper.ResolveResult(input);

            // Assert
            Assert.That(result, Is.EqualTo(input));
            Assert.That(result, Is.SameAs(input));
        }

        [Test]
        public void ResolveResult_WithCompletedTask_ReturnsTaskResult()
        {
            // Arrange
            var expectedResult = "task result";
            var task = Task.FromResult(expectedResult);

            // Act
            var result = LLMResultHelper.ResolveResult(task);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void ResolveResult_WithCompletedValueTask_ReturnsValueTaskResult()
        {
            // Arrange
            var expectedResult = 42;
            var valueTask = new ValueTask<int>(expectedResult);

            // Act
            var result = LLMResultHelper.ResolveResult(valueTask);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void ResolveResult_WithCompletedTaskOfComplexType_ReturnsTaskResult()
        {
            // Arrange
            var expectedResult = new List<string> { "item1", "item2" };
            var task = Task.FromResult(expectedResult);

            // Act
            var result = LLMResultHelper.ResolveResult(task);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(result, Is.SameAs(expectedResult));
        }

        [Test]
        public void ResolveResult_WithCompletedValueTaskOfComplexType_ReturnsValueTaskResult()
        {
            // Arrange
            var expectedResult = new Dictionary<string, int> { { "key1", 1 }, { "key2", 2 } };
            var valueTask = new ValueTask<Dictionary<string, int>>(expectedResult);

            // Act
            var result = LLMResultHelper.ResolveResult(valueTask);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
            Assert.That(result, Is.SameAs(expectedResult));
        }

        [Test]
        public void ResolveResult_WithAsyncEnumerableThatFailsSerialization_ReturnsOriginalObject()
        {
            // Arrange
            var asyncEnumerable = new MockAsyncEnumerableWithCircularReference();

            // Act
            var result = LLMResultHelper.ResolveResult(asyncEnumerable);

            // Assert
            Assert.That(result, Is.SameAs(asyncEnumerable));
        }

        [Test]
        public void ResolveResult_WithTaskWithNullResult_ReturnsNull()
        {
            // Arrange
            var task = Task.FromResult<string>(null);

            // Act
            var result = LLMResultHelper.ResolveResult(task);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ResolveResult_WithValueTaskWithNullResult_ReturnsNull()
        {
            // Arrange
            var valueTask = new ValueTask<string>((string)null);

            // Act
            var result = LLMResultHelper.ResolveResult(valueTask);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ResolveResult_WithObjectThatThrowsOnReflection_ReturnsOriginalObject()
        {
            // Arrange
            var input = new MockObjectWithBrokenProperty();

            // Act
            var result = LLMResultHelper.ResolveResult(input);

            // Assert
            Assert.That(result, Is.SameAs(input));
        }

        private class MockAsyncEnumerableWithCircularReference
        {
            public MockAsyncEnumerableWithCircularReference Self { get; }
            public string TypeName => "IAsyncEnumerable";

            public MockAsyncEnumerableWithCircularReference()
            {
                Self = this; // Circular reference will cause JSON serialization to fail
            }
        }

        private class MockObjectWithBrokenProperty
        {
            public string Result
            {
                get => throw new InvalidOperationException("Property access failed");
            }
        }
    }
}
