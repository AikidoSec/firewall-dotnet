using System.Net;
using System.Net.Sockets;
using System.Text;
using Aikido.Zen.Core;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SQLiteSampleApp;

namespace Aikido.Zen.Test.End2End
{
    [TestFixture]
    [NonParallelizable]
    public class OutboundRequestSsrfTests : WebApplicationTestBase
    {
        private TcpListener? _port3000Server;
        private TcpListener? _port4000Server;
        private static readonly byte[] HttpOkResponse = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");

        protected override Task SetupDatabaseContainers()
        {
            return Task.CompletedTask;
        }

        private static readonly string[] RedirectUrls =
        {
            "http://ssrf-redirects.testssandbox.com/ssrf-test-3",
            "http://ssrf-redirects.testssandbox.com/ssrf-test-4",
            "http://firewallssrfredirects-env-2.eba-7ifve22q.eu-north-1.elasticbeanstalk.com/ssrf-test-3",
            "http://firewallssrfredirects-env-2.eba-7ifve22q.eu-north-1.elasticbeanstalk.com/ssrf-test-4",
            "http://ssrf-rédirects.testssandbox.com/ssrf-test-3",
            "http://ssrf-rédirects.testssandbox.com/ssrf-test-4",
        };

        [OneTimeSetUp]
        public override async Task OneTimeSetUp()
        {
            await base.OneTimeSetUp();

            _port3000Server = new TcpListener(IPAddress.Loopback, 3000);
            _port4000Server = new TcpListener(IPAddress.Loopback, 4000);
            _port3000Server.Start(100);
            _port4000Server.Start(100);
            _ = RespondToRequests(_port3000Server);
            _ = RespondToRequests(_port4000Server);
        }

        [OneTimeTearDown]
        public override async Task OneTimeTearDown()
        {
            _port3000Server?.Dispose();
            _port4000Server?.Dispose();

            await base.OneTimeTearDown();
        }

        [Test]
        public async Task OutboundRequest_WhenExternalRedirectTargetsPrivateHost_Blocks()
        {
            await SendRedirectRequests(block: true);

            Assert.That(Agent.Instance.Context.AttacksDetected, Is.GreaterThan(0));
            Assert.That(Agent.Instance.Context.AttacksBlocked, Is.GreaterThan(0));
        }

        [Test]
        public async Task OutboundRequest_WhenExternalRedirectTargetsPrivateHostInDryMode_DoesNotBlock()
        {
            await SendRedirectRequests(block: false);

            Assert.That(Agent.Instance.Context.AttacksDetected, Is.GreaterThan(0));
            Assert.That(Agent.Instance.Context.AttacksBlocked, Is.EqualTo(0));
        }

        private async Task SendRedirectRequests(bool block)
        {
            await SetMode(disabled: false, block);
            Agent.Instance.ClearContext();

            SampleAppClient = CreateSampleAppFactory().CreateClient();
            await Task.Delay(250);

            foreach (var redirectUrl in RedirectUrls)
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "/api/outboundRequest?uri=" + Uri.EscapeDataString(redirectUrl));

                using var response = await SampleAppClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), redirectUrl);
                Assert.That(responseBody, Does.Contain($"Attempted outbound request to {redirectUrl}"), redirectUrl);
            }
        }

        private static async Task RespondToRequests(TcpListener listener)
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                using (client)
                {
                    try
                    {
                        await client.GetStream().WriteAsync(HttpOkResponse, 0, HttpOkResponse.Length);
                    }
                    catch (IOException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private WebApplicationFactory<SQLiteStartup> CreateSampleAppFactory()
        {
            return new WebApplicationFactory<SQLiteStartup>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddZenFirewall(options => options.UseHttpClient(MockServerClient));
                    });
                    builder.ConfigureAppConfiguration((_, _) =>
                    {
                        foreach (var envVar in SampleAppEnvironmentVariables)
                        {
                            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
                        }
                    });
                });
        }

    }
}
