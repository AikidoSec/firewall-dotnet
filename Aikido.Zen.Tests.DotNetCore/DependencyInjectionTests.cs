using System;
using System.Collections.Generic;
using Aikido.Zen.Core;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using ZenApi = Aikido.Zen.DotNetCore.Zen;

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

        [Test]
        [NonParallelizable]
        public void ApplicationStopped_ClearsCurrentContext()
        {
            using var applicationStopped = new CancellationTokenSource();
            var context = new Context();
            var contextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            contextAccessor.HttpContext.Items["Aikido.Zen.Context"] = context;
            var applicationLifetime = new Mock<IHostApplicationLifetime>();
            applicationLifetime.SetupGet(x => x.ApplicationStopped).Returns(applicationStopped.Token);
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(x => x.GetService(typeof(IOptions<AikidoOptions>)))
                .Returns(Options.Create(new AikidoOptions()));
            serviceProvider
                .Setup(x => x.GetService(typeof(IHostApplicationLifetime)))
                .Returns(applicationLifetime.Object);

            try
            {
                ZenApi.Start(serviceProvider.Object, contextAccessor);
                Assert.That(ZenApi.GetContext(), Is.SameAs(context));

                applicationStopped.Cancel();
                Assert.That(ZenApi.GetContext(), Is.Null);
            }
            finally
            {
                if (!applicationStopped.IsCancellationRequested)
                {
                    applicationStopped.Cancel();
                }
            }
        }
    }
}
