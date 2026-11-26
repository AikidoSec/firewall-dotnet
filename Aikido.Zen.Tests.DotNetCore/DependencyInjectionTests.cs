using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class DependencyInjectionTests
    {
        [TestCase("true")]
        [TestCase("1")]
        public void ZenFirewall_WhenDisabled_SkipsServiceAndMiddlewareRegistration(string setting)
        {
            var originalValue = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", setting);

                var services = new ServiceCollection();
                services.AddZenFirewall();
                Assert.That(services, Is.Empty);

                using var serviceProvider = services.BuildServiceProvider();
                var app = new ApplicationBuilder(serviceProvider);
                Assert.That(app.UseZenFirewall(), Is.SameAs(app));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalValue);
            }
        }

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

        [TestCase(false)]
        [TestCase(true)]
        [Platform("Win")]
        [NonParallelizable]
        public void UseZenFirewall_SelectsTheRunningJitUnlessConfigured(bool hasEnvironmentOverride)
        {
            var jitPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "clrjit.dll");
            var originalData = AppContext.GetData("MonoMod.JitPath");
            var originalEnvironment = Environment.GetEnvironmentVariable("MONOMOD_JitPath");
            try
            {
                AppDomain.CurrentDomain.SetData("MonoMod.JitPath", null);
                Environment.SetEnvironmentVariable("MONOMOD_JitPath", hasEnvironmentOverride ? jitPath : null);

                var builder = WebApplication.CreateBuilder();
                builder.Services.AddZenFirewall();
                using var app = builder.Build();
                app.UseRouting();
                app.UseZenFirewall();

                Assert.That(AppContext.GetData("MonoMod.JitPath"), Is.EqualTo(hasEnvironmentOverride ? null : jitPath));
            }
            finally
            {
                AppDomain.CurrentDomain.SetData("MonoMod.JitPath", originalData);
                Environment.SetEnvironmentVariable("MONOMOD_JitPath", originalEnvironment);
            }
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
