using Aikido.Zen.Core.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class StackTraceHelperTests
    {
        [Test]
        public void CleanStackTrace_ShouldRemoveZenLines()
        {
            // Arrange
            var stackTrace = "at System.Environment.GetStackTrace()\n" +
                             "at Aikido.Zen.Core.SomeClass.SomeMethod()\n" +
                             "at Other.Namespace.Class.Method()";
            var expected = "at System.Environment.GetStackTrace()\n" +
                           "at Other.Namespace.Class.Method()";

            // Act
            var result = StackTraceHelper.CleanStackTrace(stackTrace);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void CleanStackTrace_WithNullOrEmpty_ShouldReturnAsIs()
        {
            // Arrange
            string nullStackTrace = null;
            var emptyStackTrace = "";

            // Act
            var nullResult = StackTraceHelper.CleanStackTrace(nullStackTrace);
            var emptyResult = StackTraceHelper.CleanStackTrace(emptyStackTrace);

            // Assert
            Assert.That(nullResult, Is.Null);
            Assert.That(emptyResult, Is.EqualTo(""));
        }

        [Test]
        public void TruncateStackTrace_ShouldTruncate()
        {
            // Arrange
            var longStackTrace = new string('a', 100);
            var expected = new string('a', 50) + "...";

            // Act
            var result = StackTraceHelper.TruncateStackTrace(longStackTrace, 50);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TruncateStackTrace_ShortStackTrace_ShouldNotTruncate()
        {
            // Arrange
            var shortStackTrace = new string('a', 40);

            // Act
            var result = StackTraceHelper.TruncateStackTrace(shortStackTrace, 50);

            // Assert
            Assert.That(result, Is.EqualTo(shortStackTrace));
        }

        [Test]
        public void CleanedStackTrace_ShouldCleanAndTruncate()
        {
            // Arrange
            var stackTrace = "at System.Environment.GetStackTrace()\n" +
                             "at Aikido.Zen.Core.SomeClass.SomeMethod()\n" +
                             "at Other.Namespace.Class.Method()";

            var expectedCleaned = "at System.Environment.GetStackTrace()\n" +
                                  "at Other.Namespace.Class.Method()";

            // The default maxLength in CleanedStackTrace is 8096, so it shouldn't truncate this.

            // Act
            var result = StackTraceHelper.CleanedStackTrace(stackTrace);

            // Assert
            Assert.That(result, Is.EqualTo(expectedCleaned));
        }
    }
}
