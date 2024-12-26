using System.Text.Json;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class PathTraversalDetectorTests
    {
        [TestCaseSource(nameof(GetTestData))]
        public void DetectPathTraversal_ShouldDetectTraversal(string input, string path, string description, bool expectedResult)
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal(input, path);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult), description);
        }

        [Test]
        public void DetectPathTraversal_WithNullInput_ReturnsFalse()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal(null, "path");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithEmptyInput_ReturnsFalse()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("", "path");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithNullPath_ReturnsFalse()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("input", null);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithEmptyPath_ReturnsFalse()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("input", "");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithInvalidUrlEncoding_ChecksRawInput()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("%invalid%", "path");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithUrlAndUnsafePath_DetectsTraversal()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("file:///etc/passwd", "var//www//file:///etc/passwd", isUrl: true);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithUnsafePathParts_DetectsTraversal()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("file:///etc/passwd", "var//www//file:///etc/passwd", isUrl: true);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithFileUrl_ParsesCorrectly()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("file:///test.txt", "c:/file:///test.txt", isUrl: true);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithRelativeFileUrl_ParsesCorrectly()
        {
            // Act
            var result = PathTraversalDetector.DetectPathTraversal("test.txt", "test.txt", isUrl: true);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithMalformedFileUrl_HandlesException()
        {
            // This tests the catch block in ParseAsFileUrl
            // Act
            var result = PathTraversalDetector.DetectPathTraversal(
                "file:////invalid:url",
                "/some/path/file:////invalid:url", 
                isUrl: true
            );

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void DetectPathTraversal_WithRelativePathNoLeadingSlash_ParsesCorrectly()
        {
            // Tests the path.StartsWith("/") branch
            // Act
            var result = PathTraversalDetector.DetectPathTraversal(
                "relative/path/file.txt", 
                "/relative/path/file.txt", 
                isUrl: true
            );

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectPathTraversal_WithAbsolutePathNoFileScheme_ParsesCorrectly()
        {
            // Tests the file:// prefix addition branch
            // Act
            var result = PathTraversalDetector.DetectPathTraversal(
                "/absolute/path/file.txt", 
                "/absolute/path/file.txt", 
                isUrl: true
            );

            // Assert
            Assert.That(result, Is.False);
        }

        public static IEnumerable<TestCaseData> GetTestData()
        {
            var jsonData = File.ReadAllText("testdata/data.PathTraversalDetector.json");
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            foreach (var testCase in testCases)
            {
                yield return new TestCaseData(
                    testCase.Input,
                    testCase.Path,
                    testCase.Description,
                    testCase.IsTraversal
                ).SetName($"Test_{testCase.Description}");
            }
        }

        private class TestCase
        {
            public string Input { get; set; }
            public string Path { get; set; }
            public string Description { get; set; }
            public bool IsTraversal { get; set; }
        }
    }
}
