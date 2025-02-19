using System;
using System.IO;
using System.Runtime.InteropServices;
using Aikido.Zen.Core.Profiler;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class ProfilerTests
    {
        private string _mockProfilerPath;
        private string _profilerFileName;
        private string _platform;
        private string _architecture;

        [SetUp]
        public void Setup()
        {
            _mockProfilerPath = Path.Combine(Path.GetTempPath(), "AikidoProfilerTests", Guid.NewGuid().ToString());
            SetupPlatformSpecificValues();
            Directory.CreateDirectory(_mockProfilerPath);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_mockProfilerPath))
                {
                    Directory.Delete(_mockProfilerPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }

            // Reset environment variables
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", null);
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", null);
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", null);
        }

        private void SetupPlatformSpecificValues()
        {
            _architecture = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => throw new PlatformNotSupportedException()
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = "windows";
                _profilerFileName = $"Aikido.Zen.Profiler.{_platform}.{_architecture}.dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = "linux";
                _profilerFileName = $"libAikido.Zen.Profiler.{_platform}.{_architecture}.so";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _platform = "osx";
                _profilerFileName = $"libAikido.Zen.Profiler.{_platform}.{_architecture}.dylib";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        [Test]
        public void GetProfilerPath_ShouldReturnCorrectPath()
        {
            // Arrange
            string expectedPath = Path.Combine(_mockProfilerPath, _profilerFileName);
            File.WriteAllText(expectedPath, ""); // Create empty file

            // Act
            string profilerPath = ProfilerLoader.GetProfilerPath(_mockProfilerPath);

            // Assert
            Assert.That(profilerPath, Is.Not.Null);
            Assert.That(File.Exists(profilerPath), Is.True);
            Assert.That(profilerPath, Does.Contain(_mockProfilerPath));
            Assert.That(profilerPath, Does.EndWith(_profilerFileName));
        }

        [Test]
        public void GetProfilerPath_WithInvalidPath_ShouldThrowFileNotFoundException()
        {
            // Arrange
            string invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act & Assert
            Assert.That(() => ProfilerLoader.GetProfilerPath(invalidPath),
                Throws.TypeOf<FileNotFoundException>());
        }

        [Test]
        public void ProfilerManager_Initialize_WithEmptyPath_ShouldThrow()
        {
            // Arrange
            var manager = new ProfilerManager();

            // Act & Assert
            Assert.That(() => manager.Initialize(string.Empty),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
