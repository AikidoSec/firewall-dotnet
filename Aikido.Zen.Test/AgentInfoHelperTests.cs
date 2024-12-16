using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;

namespace Aikido.Zen.Test
{
    public class AgentInfoHelperTests
    {
        private string _originalBlockingValue;
        private string _originalLambdaValue;
        private string _originalAzureValue;

        [SetUp]
        public void Setup()
        {
            // Store original environment variables
            _originalBlockingValue = Environment.GetEnvironmentVariable("AIKIDO_BLOCKING");
            _originalLambdaValue = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
            _originalAzureValue = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        }

        [TearDown]
        public void Cleanup()
        {
            // Restore original environment variables
            SetEnvironmentVariable("AIKIDO_BLOCKING", _originalBlockingValue);
            SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", _originalLambdaValue);
            SetEnvironmentVariable("WEBSITE_INSTANCE_ID", _originalAzureValue);
        }

        [Test]
        public void GetInfo_ShouldReturnValidAgentInfo()
        {
            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.Multiple(() =>
            {
                // Basic properties
                Assert.That(agentInfo.Hostname, Is.EqualTo(Environment.MachineName));
                Assert.That(agentInfo.Library, Is.EqualTo("firewall-dotnet"));
                Assert.That(agentInfo.Version, Is.EqualTo("1.0.0"));
                Assert.That(agentInfo.IpAddress, Is.Not.Null);

                // OS Info
                Assert.That(agentInfo.Os.Version, Is.EqualTo(Environment.OSVersion.VersionString));
                Assert.That(agentInfo.Os.Name, Is.EqualTo(Environment.OSVersion.Platform.ToString()));

                // Platform Info
                Assert.That(agentInfo.Platform.Version, Is.EqualTo(Environment.Version.ToString()));
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo(Environment.Version.Major >= 5 ? "core" : "framework"));
            });
        }

        [Test]
        public void DryMode_ShouldBeTrueByDefault()
        {
            // Arrange
            SetEnvironmentVariable("AIKIDO_BLOCKING", null);

            // Act
            bool isDryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(isDryMode, Is.True);
        }

        [Test]
        public void DryMode_ShouldBeFalseWhenBlockingEnabled()
        {
            // Arrange
            SetEnvironmentVariable("AIKIDO_BLOCKING", "true");

            // Act
            bool isDryMode = EnvironmentHelper.DryMode;

            // Assert
            Assert.That(isDryMode, Is.False);
        }

        [Test]
        public void GetInfo_ServerlessDetection_AWS()
        {
            // Arrange
            SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.That(agentInfo.Serverless, Is.True);
        }

        [Test]
        public void GetInfo_ServerlessDetection_Azure()
        {
            // Arrange
            SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "test-instance");

            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.That(agentInfo.Serverless, Is.True);
        }

        [Test]
        public void GetInfo_NotServerless_WhenNoServerlessEnvironmentVariables()
        {
            // Arrange
            SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
            SetEnvironmentVariable("WEBSITE_INSTANCE_ID", null);

            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.That(agentInfo.Serverless, Is.False);
        }

        [Test]
        public void GetInfo_PlatformArchitecture_Core()
        {
            // This test is environment-dependent
            if (Environment.Version.Major >= 5)
            {
                // Act
                var agentInfo = AgentInfoHelper.GetInfo();

                // Assert
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo("core"));
            }
        }

        [Test]
        public void GetInfo_PlatformArchitecture_Framework()
        {
            // This test is environment-dependent
            if (Environment.Version.Major < 5)
            {
                // Act
                var agentInfo = AgentInfoHelper.GetInfo();

                // Assert
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo("framework"));
            }
        }

        private void SetEnvironmentVariable(string variable, string value)
        {
            if (value == null)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
            else
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }
}