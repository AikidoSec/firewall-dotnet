using System;
using System.Collections.Generic;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class DependencyInjectionTests
    {
        [Test]
        public void UseZenFirewall_WithoutUseRouting_Throws()
        {
            var originalValue = Environment.GetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK");

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", null);

                var services = new ServiceCollection();
                var serviceProvider = services.BuildServiceProvider();
                var app = new ApplicationBuilder(serviceProvider);

                var exception = Assert.Throws<InvalidOperationException>(() => app.UseZenFirewall());
                Assert.That(exception?.Message, Does.Contain("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK=true"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", originalValue);
            }
        }

        [Test]
        public void UseZenFirewall_WithUseRouting_DoesNotThrow()
        {
            var services = new ServiceCollection();
            services.AddRouting();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddZenFirewall();

            var serviceProvider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(serviceProvider);

            app.UseRouting();

            Assert.DoesNotThrow(() => app.UseZenFirewall());
        }

        [Test]
        public void UseZenFirewall_WithoutUseRouting_WhenEndpointRoutingCheckDisabled_DoesNotThrow()
        {
            var originalValue = Environment.GetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK");

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", "true");

                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                services.AddZenFirewall();

                var serviceProvider = services.BuildServiceProvider();
                var app = new ApplicationBuilder(serviceProvider);

                Assert.DoesNotThrow(() => app.UseZenFirewall());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK", originalValue);
            }
        }
    }
}
