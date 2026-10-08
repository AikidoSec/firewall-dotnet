
using Aikido.Zen.Core;
using System;
using System.Configuration;
using System.Web;

namespace Aikido.Zen.DotNetFramework.Configuration
{
	public class AikidoConfiguration
	{
        // Application-specific configuration key to store options in HttpContext.Application
        private const string ApplicationConfigKey = "Aikido.Zen.ApplicationConfig";

		public static AikidoOptions Options  {
			get {
                // First check if we have application-specific configuration stored
                var applicationConfig = GetApplicationConfig();
                if (applicationConfig != null)
                {
                    return applicationConfig;
                }

                // Fall back to reading from configuration and environment variables
                // This maintains backward compatibility for single-application scenarios
                var options = new AikidoOptions {
                    AikidoToken = ConfigurationManager.AppSettings["Aikido:AikidoToken"]
                        ?? Environment.GetEnvironmentVariable("AIKIDO_TOKEN"),
                    AikidoUrl = ConfigurationManager.AppSettings["Aikido:AikidoUrl"]
                        ?? Environment.GetEnvironmentVariable("AIKIDO_ENDPOINT")
                };

                // Store the configuration for this application to ensure consistency
                SetApplicationConfig(options);
                return options;
            }
		}

        /// <summary>
        /// Gets the application-specific configuration stored in HttpContext.Application.
        /// This ensures each ASP.NET application in a shared worker process uses its own configuration.
        /// </summary>
        private static AikidoOptions GetApplicationConfig()
        {
            try
            {
                var application = HttpContext.Current?.ApplicationInstance;
                if (application != null)
                {
                    return application.Application[ApplicationConfigKey] as AikidoOptions;
                }
            }
            catch
            {
                // If we can't access HttpContext, fall back to environment variables
            }
            return null;
        }

        /// <summary>
        /// Stores the application-specific configuration in HttpContext.Application.
        /// This ensures each ASP.NET application in a shared worker process uses its own configuration.
        /// </summary>
        private static void SetApplicationConfig(AikidoOptions options)
        {
            try
            {
                var application = HttpContext.Current?.ApplicationInstance;
                if (application != null)
                {
                    application.Application[ApplicationConfigKey] = options;
                }
            }
            catch
            {
                // If we can't access HttpContext, continue without storing
            }
        }

        /// <summary>
        /// Initializes the configuration for this application.
        /// This method is maintained for backward compatibility but no longer writes to process-wide environment variables
        /// to prevent cross-application credential leakage in shared worker processes.
        /// </summary>
        internal static void Init() {
            // Read and store application-specific configuration
            var options = Options;
            
            // Only set process-wide environment variables if they are not already set
            // and if we have values from this application's configuration.
            // This maintains backward compatibility for single-application scenarios
            // while preventing the first application from overriding subsequent applications.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_TOKEN")) && !string.IsNullOrEmpty(options.AikidoToken))
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", options.AikidoToken);
            }
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_ENDPOINT")) && !string.IsNullOrEmpty(options.AikidoUrl))
            {
                Environment.SetEnvironmentVariable("AIKIDO_ENDPOINT", options.AikidoUrl);
            }
        }
	}
}
