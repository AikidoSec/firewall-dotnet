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
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(serviceProvider);

            Assert.Throws<InvalidOperationException>(() => app.UseZenFirewall());
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
    }
}
