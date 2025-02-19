using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Profiler;
using Aikido.Zen.DotNetCore.Profiler;
using Aikido.Zen.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore.Profiler
{
    [TestFixture]
    public class ProfilerExtensionsTests : IDisposable
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
        }

        [Test]
        public void AddAikidoProfiler_ShouldRegisterServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddAikidoProfiler(_mockProfilerPath);

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var profilerManager = serviceProvider.GetService<ProfilerManager>();

            Assert.That(profilerManager, Is.Not.Null);
            Assert.That(profilerManager.IsInitialized, Is.True);
        }

        [Test]
        public async Task ProfilerHostedService_ShouldHandleShutdown()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddAikidoProfiler(_mockProfilerPath);
            var serviceProvider = services.BuildServiceProvider();

            var hostedService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                .OfType<ProfilerHostedService>()
                .First();

            // Act
            await hostedService.StopAsync(CancellationToken.None);

            // Assert
            var profilerManager = serviceProvider.GetService<ProfilerManager>();
            Assert.That(profilerManager.IsInitialized, Is.False);
        }

        [Test]
        public void AddAikidoProfiler_WithDefaultPath_ShouldUseAppBaseDirectory()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockProfilerPath = ProfilerTestHelper.SetupMockProfilerFiles();
            var expectedPath = System.IO.Path.Combine(AppContext.BaseDirectory, "profiler");

            try
            {
                // Copy mock files to expected location
                System.IO.Directory.CreateDirectory(expectedPath);
                foreach (var file in System.IO.Directory.GetFiles(mockProfilerPath, "*.*", System.IO.SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(mockProfilerPath.Length + 1);
                    var targetPath = System.IO.Path.Combine(expectedPath, relativePath);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath));
                    System.IO.File.Copy(file, targetPath, true);
                }

                // Act
                services.AddAikidoProfiler();
                var serviceProvider = services.BuildServiceProvider();
                var profilerManager = serviceProvider.GetService<ProfilerManager>();

                // Assert
                Assert.That(profilerManager, Is.Not.Null);
                Assert.That(profilerManager.IsInitialized, Is.True);
            }
            finally
            {
                // Cleanup
                if (System.IO.Directory.Exists(expectedPath))
                {
                    System.IO.Directory.Delete(expectedPath, true);
                }
            }
        }
    }
}
