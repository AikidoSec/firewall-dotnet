using System.Runtime.InteropServices;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test.Helpers
{
    public class AgentInfoHelperTests
    {
        private string _originalBlockingValue;
        private string _originalLambdaValue;
        private string _originalAzureValue;

        [SetUp]
        public void Setup ()
        {
            // Store original environment variables
            _originalBlockingValue = Environment.GetEnvironmentVariable("AIKIDO_BLOCK");
            _originalLambdaValue = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
            _originalAzureValue = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        }

        [TearDown]
        public void Cleanup ()
        {
            // Restore original environment variables
            SetEnvironmentVariable("AIKIDO_BLOCK", _originalBlockingValue);
            SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", _originalLambdaValue);
            SetEnvironmentVariable("WEBSITE_INSTANCE_ID", _originalAzureValue);
        }

        [Test]
        public void GetInfo_ShouldReturnValidAgentInfo ()
        {
            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.Multiple(() =>
            {
                // Basic properties
                Assert.That(agentInfo.Hostname, Is.EqualTo(Environment.MachineName));
                Assert.That(agentInfo.Library, Is.EqualTo("firewall-dotnet"));
                Assert.That(agentInfo.Version, Is.EqualTo(AgentInfoHelper.CleanVersion(typeof(AgentInfoHelper).Assembly.GetName().Version!.ToString())));
                Assert.That(agentInfo.IpAddress, Is.Not.Null);

                // OS Info
                Assert.That(agentInfo.Os.Version, Is.EqualTo(Environment.OSVersion.VersionString));
                Assert.That(agentInfo.Os.Name, Is.EqualTo(Environment.OSVersion.Platform.ToString()));

                // Platform Info
                Assert.That(agentInfo.Platform.Version, Is.EqualTo(Environment.Version.ToString()));
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo(RuntimeInformation.ProcessArchitecture.ToString()));
            });
        }

        [Test]
        public void GetInfo_ServerlessDetection_AWS ()
        {
            // Arrange
            SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "test-function");

            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.That(agentInfo.Serverless, Is.True);
        }

        [Test]
        public void GetInfo_ServerlessDetection_Azure ()
        {
            // Arrange
            SetEnvironmentVariable("WEBSITE_INSTANCE_ID", "test-instance");

            // Act
            var agentInfo = AgentInfoHelper.GetInfo();

            // Assert
            Assert.That(agentInfo.Serverless, Is.True);
        }

        [Test]
        public void GetInfo_NotServerless_WhenNoServerlessEnvironmentVariables ()
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
        public void GetInfo_PlatformArchitecture_Core ()
        {
            // This test is environment-dependent
            if (Environment.Version.Major >= 5)
            {
                // Act
                var agentInfo = AgentInfoHelper.GetInfo();

                // Assert
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo(RuntimeInformation.ProcessArchitecture.ToString()));
            }
        }

        [Test]
        public void GetInfo_PlatformArchitecture_Framework ()
        {
            // This test is environment-dependent
            if (Environment.Version.Major < 5)
            {
                // Act
                var agentInfo = AgentInfoHelper.GetInfo();

                // Assert
                Assert.That(agentInfo.Platform.Arch, Is.EqualTo(RuntimeInformation.ProcessArchitecture.ToString()));
            }
        }

        private void SetEnvironmentVariable (string variable, string value)
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

        [Test]
        public void CleanVersion_ShouldRemoveBuildNumber ()
        {
            var version = "1.2.3+4";
            var otherVersion = "1.2.3.0";
            var cleanedVersion = AgentInfoHelper.CleanVersion(version);
            var cleanedOtherVersion = AgentInfoHelper.CleanVersion(otherVersion);
            Assert.That(cleanedVersion, Is.EqualTo("1.2.3"));
            Assert.That(cleanedOtherVersion, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void CleanVersion_ShouldRemovePrereleaseVersion ()
        {
            var version = "1.2.3-alpha";
            var cleanedVersion = AgentInfoHelper.CleanVersion(version);
            Assert.That(cleanedVersion, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void CleanVersion_ShouldRemoveBuildNumberAndPrereleaseVersion ()
        {
            var version = "1.2.3+4-alpha";
            var cleanedVersion = AgentInfoHelper.CleanVersion(version);
            Assert.That(cleanedVersion, Is.EqualTo("1.2.3"));
        }
    }
}
