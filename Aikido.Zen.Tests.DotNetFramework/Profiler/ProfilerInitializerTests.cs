using System;
using System.IO;
using Aikido.Zen.DotNetFramework.Profiler;
using Aikido.Zen.Tests.Helpers;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetFramework.Profiler
{
    [TestFixture]
    public class ProfilerInitializerTests : IDisposable
    {
        private string _mockProfilerPath;

        [SetUp]
        public void Setup()
        {
            _mockProfilerPath = ProfilerTestHelper.SetupMockProfilerFiles();
        }

        public void Dispose()
        {
            ProfilerTestHelper.CleanupMockProfilerFiles(_mockProfilerPath);
            ProfilerInitializer.Shutdown(); // Ensure cleanup between tests
        }

        [Test]
        public void Initialize_ShouldSucceed()
        {
            // Act
            ProfilerInitializer.Initialize(_mockProfilerPath);

            // Assert
            Assert.That(ProfilerInitializer.Current, Is.Not.Null);
            Assert.That(ProfilerInitializer.Current.IsInitialized, Is.True);
        }

        [Test]
        public void Initialize_CalledTwice_ShouldNotThrow()
        {
            // Arrange
            ProfilerInitializer.Initialize(_mockProfilerPath);

            // Act & Assert
            Assert.That(() => ProfilerInitializer.Initialize(_mockProfilerPath), Throws.Nothing);
        }

        [Test]
        public void Current_WhenNotInitialized_ShouldThrow()
        {
            // Act & Assert
            Assert.That(() => ProfilerInitializer.Current,
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Initialize_WithDefaultPath_ShouldUseAppDomainBase()
        {
            // Arrange
            var expectedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiler");

            try
            {
                // Copy mock files to expected location
                Directory.CreateDirectory(expectedPath);
                foreach (var file in Directory.GetFiles(_mockProfilerPath, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(_mockProfilerPath.Length + 1);
                    var targetPath = Path.Combine(expectedPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    File.Copy(file, targetPath, true);
                }

                // Act
                ProfilerInitializer.Initialize();

                // Assert
                Assert.That(ProfilerInitializer.Current, Is.Not.Null);
                Assert.That(ProfilerInitializer.Current.IsInitialized, Is.True);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(expectedPath))
                {
                    Directory.Delete(expectedPath, true);
                }
            }
        }

        [Test]
        public void Shutdown_ShouldCleanupAndAllowReinitialization()
        {
            // Arrange
            ProfilerInitializer.Initialize(_mockProfilerPath);
            Assert.That(ProfilerInitializer.Current.IsInitialized, Is.True);

            // Act
            ProfilerInitializer.Shutdown();

            // Assert
            Assert.That(() => ProfilerInitializer.Current,
                Throws.TypeOf<InvalidOperationException>());

            // Should be able to initialize again
            ProfilerInitializer.Initialize(_mockProfilerPath);
            Assert.That(ProfilerInitializer.Current.IsInitialized, Is.True);
        }
    }
}
