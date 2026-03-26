using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
using Moq;

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
            // Act
            bool result = PathTraversalHelper.DetectPathTraversal("test.txt", null, ModuleName, Operation);

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
            bool result = PathTraversalHelper.DetectPathTraversal(filename, _context, ModuleName, Operation);

            // Assert
            Assert.That(result, Is.EqualTo(expectedAttack));
            Assert.That(_context.AttackDetected, Is.EqualTo(expectedAttack));
        }

        [Test]
        public async Task DetectPathTraversal_WhenAttackDetected_ReportsFilenameMetadata()
        {
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            _context.ParsedUserInput.Clear();
            _context.ParsedUserInput.Add("query.file", "../test.txt");

            var filename = "/var/www/data/../test.txt";

            PathTraversalHelper.DetectPathTraversal(filename, _context, ModuleName, Operation);
            await Task.Delay(150);

            reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == "path_traversal" &&
                        a.Attack.Path == ".file" &&
                        a.Attack.Metadata.ContainsKey("filename") &&
                        (string)a.Attack.Metadata["filename"] == filename &&
                        !a.Attack.Metadata.ContainsKey("path"))),
                Times.Once);
        }
    }
}
