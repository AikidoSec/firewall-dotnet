using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class BlazorDependencyInjectionTests
    {
        [Test]
        public void UseZenFirewall_WithDefaultBlazorPipeline_DoesNotThrow()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            builder.Services.AddZenFirewall();
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();
            app.UseStaticFiles();
            app.UseAntiforgery();

            Assert.DoesNotThrow(() => app.UseZenFirewall());
        }

        [Test]
        public void UseZenFirewall_WithBlazorPipelineAndUseRouting_DoesNotThrow()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            builder.Services.AddZenFirewall();
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();
            app.UseRouting();
            app.UseStaticFiles();
            app.UseAntiforgery();

            Assert.DoesNotThrow(() => app.UseZenFirewall());
        }
    }
}
