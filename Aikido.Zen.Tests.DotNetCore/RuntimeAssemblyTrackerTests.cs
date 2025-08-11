using Aikido.Zen.Core;
using Aikido.Zen.DotNetCore.RuntimeSca;
using Microsoft.Extensions.DependencyModel;
using Moq;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.Tests.DotNetCore
{
    [TestFixture]
    public class RuntimeAssemblyTrackerTests
    {
        private Mock<IAgent> _mockAgent;
        private Mock<IDependencyContextProvider> _mockDependencyContext;
        private Mock<IFileVersionInfoProvider> _mockFileVersionInfo;
        private RuntimeAssemblyTracker _tracker;
        private Assembly _testAssembly;
        private FileVersionInfo _testFileVersionInfo;

        [SetUp]
        public void SetUp()
        {
            _mockAgent = new Mock<IAgent>();
            _mockDependencyContext = new Mock<IDependencyContextProvider>();
            _mockFileVersionInfo = new Mock<IFileVersionInfoProvider>();
            
            // Get a real assembly for testing
            _testAssembly = Assembly.GetExecutingAssembly();
            _testFileVersionInfo = FileVersionInfo.GetVersionInfo(_testAssembly.Location);
            
            _tracker = new RuntimeAssemblyTracker(
                _mockAgent.Object,
                _mockDependencyContext.Object,
                _mockFileVersionInfo.Object);
        }

        [Test]
        public void AddAssembly_WithNullAssembly_ShouldNotAddPackage()
        {
            // Act
            _tracker.AddAssembly(null);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithEmptyLocation_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", "");

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithNonPackageLibrary_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            var expectedFileVersion = $"{_testFileVersionInfo.FileMajorPart}.{_testFileVersionInfo.FileMinorPart}.{_testFileVersionInfo.FileBuildPart}.{_testFileVersionInfo.FilePrivatePart}";
            var mockLibrary = CreateMockRuntimeLibrary("TestProject", "1.0.0", "project", "TestAssembly.dll", "1.0.0.0", expectedFileVersion);
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithMatchingPackage_ShouldAddRuntimePackage()
        {
            // Arrange
            var actualFileName = Path.GetFileName(_testAssembly.Location);
            var mockAssembly = CreateTestAssembly(actualFileName, "1.0.0.0", _testAssembly.Location);
            var expectedFileVersion = $"{_testFileVersionInfo.FileMajorPart}.{_testFileVersionInfo.FileMinorPart}.{_testFileVersionInfo.FileBuildPart}.{_testFileVersionInfo.FilePrivatePart}";
            var mockLibrary = CreateMockRuntimeLibrary("TestPackage", "1.0.0", "package", actualFileName, "1.0.0.0", expectedFileVersion);
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage("TestPackage", "1.0.0"), Times.Once);
        }

        [Test]
        public void AddAssembly_WithNoMatchingLibrary_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            
            // Setup empty runtime libraries
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(Array.Empty<RuntimeLibrary>());
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithMatchingLibrary_ShouldAddRuntimePackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            var expectedFileVersion = $"{_testFileVersionInfo.FileMajorPart}.{_testFileVersionInfo.FileMinorPart}.{_testFileVersionInfo.FileBuildPart}.{_testFileVersionInfo.FilePrivatePart}";
            
            // Use the actual file name from the test assembly location
            var actualFileName = Path.GetFileName(_testAssembly.Location);
            var mockLibrary = CreateMockRuntimeLibrary("TestPackage", "1.0.0", "package", actualFileName, "1.0.0.0", expectedFileVersion);
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage("TestPackage", "1.0.0"), Times.Once);
        }

        [Test]
        public void AddAssembly_WithMismatchedVersions_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            var expectedFileVersion = $"{_testFileVersionInfo.FileMajorPart}.{_testFileVersionInfo.FileMinorPart}.{_testFileVersionInfo.FileBuildPart}.{_testFileVersionInfo.FilePrivatePart}";
            var mockLibrary = CreateMockRuntimeLibrary("TestPackage", "1.0.0", "package", "TestAssembly.dll", "2.0.0.0", expectedFileVersion);
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithMismatchedFileVersions_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            var mockLibrary = CreateMockRuntimeLibrary("TestPackage", "1.0.0", "package", "TestAssembly.dll", "1.0.0.0", "2.0.0.0");
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithDifferentFileName_ShouldNotAddPackage()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", _testAssembly.Location);
            var expectedFileVersion = $"{_testFileVersionInfo.FileMajorPart}.{_testFileVersionInfo.FileMinorPart}.{_testFileVersionInfo.FileBuildPart}.{_testFileVersionInfo.FilePrivatePart}";
            var mockLibrary = CreateMockRuntimeLibrary("TestPackage", "1.0.0", "package", "DifferentAssembly.dll", "1.0.0.0", expectedFileVersion);
            
            _mockDependencyContext.Setup(x => x.GetRuntimeLibraries()).Returns(new[] { mockLibrary });
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo(_testAssembly.Location)).Returns(_testFileVersionInfo);

            // Act
            _tracker.AddAssembly(mockAssembly);

            // Assert
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddAssembly_WithFileVersionInfoException_ShouldNotCrash()
        {
            // Arrange
            var mockAssembly = CreateTestAssembly("TestAssembly.dll", "1.0.0.0", "C:\\nonexistent\\path\\TestAssembly.dll");
            
            _mockFileVersionInfo.Setup(x => x.GetVersionInfo("C:\\nonexistent\\path\\TestAssembly.dll"))
                .Throws(new FileNotFoundException());

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => _tracker.AddAssembly(mockAssembly));
            
            // Should not have called AddRuntimePackage
            _mockAgent.Verify(x => x.AddRuntimePackage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // Helper method to create a test assembly
        private Assembly CreateTestAssembly(string fileName, string version, string location)
        {
            return new TestAssembly(fileName, version, location);
        }

        private RuntimeLibrary CreateMockRuntimeLibrary(string name, string version, string type, 
            string fileName = "TestAssembly.dll", string assemblyVersion = "1.0.0.0", string fileVersion = "1.0.0.0")
        {
            // Create a real RuntimeLibrary instance with mocked data
            var runtimeFiles = new List<RuntimeFile>
            {
                new RuntimeFile($"lib/net6.0/{fileName}", assemblyVersion, fileVersion)
            };

            var runtimeAssemblyGroup = new RuntimeAssetGroup("", runtimeFiles);
            var runtimeAssemblyGroups = new List<RuntimeAssetGroup> { runtimeAssemblyGroup };

            return new RuntimeLibrary(
                type,
                name,
                version,
                "",
                runtimeAssemblyGroups,
                new List<RuntimeAssetGroup>(),
                new List<ResourceAssembly>(),
                new List<Dependency>(),
                serviceable: false);
        }
    }

    // Test double for Assembly since it can't be mocked
    public class TestAssembly : Assembly
    {
        private readonly string _fileName;
        private readonly string _version;
        private readonly string _location;

        public TestAssembly(string fileName, string version, string location)
        {
            _fileName = fileName;
            _version = version;
            _location = location;
        }

        public override string Location => _location;

        public override AssemblyName GetName()
        {
            var assemblyName = new AssemblyName
            {
                Version = new Version(_version)
            };
            return assemblyName;
        }

        public override string FullName => $"{_fileName}, Version={_version}";
    }
}
