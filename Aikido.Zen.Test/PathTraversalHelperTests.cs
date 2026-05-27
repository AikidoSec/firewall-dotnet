using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;
using System.Reflection;
using System.Text.Json;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class PathTraversalHelperTests
    {
        private Context _context;
        private Context? _activeContext;
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
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
        }

        [Test]
        public void DetectPathTraversal_WithNullContext_ReturnsFalse()
        {
            // Act
            bool result = DetectPathTraversal("test.txt", null, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.False);
        }

        [TestCase("../test.txt", true, Description = "Detects traversal in single path")]
        [TestCase("safe.txt", false, Description = "Passes safe single path")]
        public void DetectPathTraversal_WithSingleFilename(string filename, bool expectedAttack)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _context.ParsedUserInput.Add("test", filename);

            // Act
            bool result = DetectPathTraversal(filename, _context, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.EqualTo(expectedAttack));
            Assert.That(_context.AttackDetected, Is.EqualTo(expectedAttack));
        }

        [TestCase("..%2Fsecrets%2Fkey.txt", Description = "Single-encoded traversal")]
        [TestCase("%252e%252e%252fsecrets%252fkey.txt", Description = "Double-encoded traversal")]
        public async Task DetectPathTraversal_WithUriDecodedParsedInput_DetectsAttack(string encodedInput)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            using var bodyStream = new MemoryStream();
            var parsed = await HttpHelper.ReadAndFlattenHttpDataAsync(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "path", encodedInput } },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                bodyStream,
                "text/plain");
            _context.ParsedUserInput = parsed.FlattenedData;

            var filename = "wwwroot/blogs/../secrets/key.txt";

            bool result = DetectPathTraversal(filename, _context, ModuleName, Operation);

            Assert.That(parsed.FlattenedData["query.path"], Is.EqualTo(encodedInput));
            Assert.That(parsed.FlattenedData["query.path|decoded"], Is.EqualTo("../secrets/key.txt"));
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [TestCaseSource(nameof(GetUriEncodedPathTraversalTestData))]
        public async Task DetectPathTraversal_WithUriEncodedParsedInput_MatchesDetectorFixture(string encodedInput, string decodedInput, string path, string description, bool expectedAttack)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            using var bodyStream = new MemoryStream();
            var parsed = await HttpHelper.ReadAndFlattenHttpDataAsync(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "path", encodedInput } },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                bodyStream,
                "text/plain");
            _context.ParsedUserInput = parsed.FlattenedData;

            bool result = DetectPathTraversal(path, _context, ModuleName, Operation);

            Assert.That(parsed.FlattenedData["query.path"], Is.EqualTo(encodedInput));
            if (encodedInput == decodedInput)
            {
                Assert.That(parsed.FlattenedData.ContainsKey("query.path|decoded"), Is.False);
            }
            else
            {
                Assert.That(parsed.FlattenedData["query.path|decoded"], Is.EqualTo(decodedInput));
            }
            Assert.That(result, Is.EqualTo(expectedAttack), description);
            Assert.That(_context.AttackDetected, Is.EqualTo(expectedAttack), description);
        }

        [Test]
        public async Task DetectPathTraversal_WhenAttackDetected_ReportsFilenameMetadata()
        {
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            _context.ParsedUserInput.Clear();
            _context.ParsedUserInput.Add("query.file", "../test.txt");

            var filename = "/var/www/data/../test.txt";

            DetectPathTraversal(filename, _context, ModuleName, Operation);
            await Task.Delay(150);

            reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == "path_traversal" &&
                        a.Attack.Path == ".file" &&
                        a.Attack.Metadata.ContainsKey("filename") &&
                        (string)a.Attack.Metadata["filename"] == filename &&
                        !a.Attack.Metadata.ContainsKey("path")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private bool DetectPathTraversal(string path, Context context, string moduleName, string operation)
        {
            _activeContext = context;
            var method = typeof(PathTraversalHelperTests).GetMethod(nameof(DetectPathTraversal), BindingFlags.Instance | BindingFlags.NonPublic);
            var result = PathTraversalHelper.DetectPathTraversal(path, context);
            Inspector.Inspect(
                method,
                "fs_op",
                _ => result);
            return result.AttackKind.HasValue;
        }

        public static IEnumerable<TestCaseData> GetUriEncodedPathTraversalTestData()
        {
            var jsonData = File.ReadAllText("testdata/data.PathTraversalDetector.json");
            var testCases = JsonSerializer.Deserialize<List<PathTraversalTestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<PathTraversalTestCase>();

            for (var i = 0; i < testCases.Count; i++)
            {
                var testCase = testCases[i];
                var input = testCase.Input ?? string.Empty;
                var path = testCase.Path ?? string.Empty;
                var onceEncoded = Uri.EscapeDataString(input);
                var twiceEncoded = Uri.EscapeDataString(onceEncoded);

                yield return new TestCaseData(
                    onceEncoded,
                    input,
                    path,
                    testCase.Description,
                    testCase.IsTraversal
                ).SetName($"DecodedFlow_Once_{i}_{testCase.Description}");

                yield return new TestCaseData(
                    twiceEncoded,
                    input,
                    path,
                    testCase.Description,
                    testCase.IsTraversal
                ).SetName($"DecodedFlow_Twice_{i}_{testCase.Description}");
            }
        }

        private class PathTraversalTestCase
        {
            public string? Input { get; set; }
            public string? Path { get; set; }
            public string? Description { get; set; }
            public bool IsTraversal { get; set; }
        }
    }
}
