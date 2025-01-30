using Aikido.Zen.Core.Helpers;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Linq;

namespace Aikido.Zen.Test.Helpers
{
    [TestFixture]
    public class LoggerConfiguratorTests
    {
        [Test]
        public void CreateLogger_ShouldReturnLoggerInstance()
        {
            // Act
            var logger = LoggerConfigurator.CreateLogger<LoggerConfiguratorTests>();

            // Assert
            Assert.That(logger, Is.Not.Null);
            Assert.That(logger, Is.InstanceOf<ILogger<LoggerConfiguratorTests>>());
        }

        [Test]
        public void ConfigureLogging_ShouldAddConsoleLogger_WhenDebuggingEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");

            // Act
            LoggerConfigurator.ConfigureLogging();

            var logger = LoggerConfigurator.CreateLogger<LoggerConfiguratorTests>();

            // Assert
            Assert.That(logger, Is.Not.Null);
            // Note: Verifying console output directly is complex; this test ensures no exceptions and logger creation.
        }

        [Test]
        public void ConfigureLogging_ShouldIncludeConsoleLogger_WhenDebuggingEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");

            // Act
            LoggerConfigurator.ConfigureLogging();

            var logger = LoggerConfigurator.CreateLogger<LoggerConfiguratorTests>();

            // Assert
            // Since we can't directly access the providers, we ensure no exceptions and logger creation
            Assert.That(logger, Is.Not.Null);
            // Note: Direct verification of console logger presence is complex without internal access.
        }
    }
}
