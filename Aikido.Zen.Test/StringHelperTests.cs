using Aikido.Zen.Core.Helpers;
using NUnit.Framework;
using System;

namespace Aikido.Zen.Test
{
    internal class StringHelperTests
    {
        [TestCase("hello world", "world", true)]
        [TestCase("hello world", "worlds", false)]
        [TestCase("hello world", "", true)]
        [TestCase("hello world", "hello world", true)]
        [TestCase("hello world", "hello world!", false)]
        public void Contains_ShouldReturnExpectedResult(string source, string value, bool expectedResult)
        {
            // Arrange
            var sourceSpan = source.AsSpan();
            var valueSpan = value.AsSpan();

            // Act
            var result = sourceSpan.Contains(valueSpan);

            // Assert
            Assert.AreEqual(expectedResult, result);
        }

        [TestCase("segment1/segment2", "segment1", "segment2")]
        [TestCase("segment1", "segment1", "")]
        [TestCase("", "", "")]
        public void GetNextSegment_ShouldReturnExpectedResult(string input, string expectedSegment, string expectedRemainder)
        {
            // Arrange
            var inputSpan = input.AsSpan();

            // Act
            var segment = inputSpan.GetNextSegment(out var remainder);

            // Assert
            Assert.AreEqual(expectedSegment, segment.ToString());
            Assert.AreEqual(expectedRemainder, remainder.ToString());
        }

        [TestCase("{parameter}", true)]
        [TestCase("{parameter", false)]
        [TestCase("parameter}", false)]
        [TestCase("parameter", false)]
        [TestCase("thing={value}", false)]
        public void IsRouteParameter_ShouldReturnExpectedResult(string input, bool expectedResult)
        {
            // Arrange
            var inputSpan = input.AsSpan();

            // Act
            var result = inputSpan.IsRouteParameter();

            // Assert
            Assert.AreEqual(expectedResult, result);
        }
    }
}
