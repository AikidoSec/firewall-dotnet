using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    [NonParallelizable]
    public class OutboundRequestInnerSinkTests
    {
        private Mock<IReportingAPIClient> _reportingApiMock = null!;
        private Mock<IRuntimeAPIClient> _runtimeApiMock = null!;
        private Agent _agent = null!;
        private Context? _activeContext;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3001");

            _reportingApiMock = new Mock<IReportingAPIClient>();
            _reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            _runtimeApiMock = new Mock<IRuntimeAPIClient>();
            _runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            _agent = Agent.NewInstance(ZenApiMock.CreateMock(_reportingApiMock.Object, _runtimeApiMock.Object).Object);
            _agent.ClearContext();
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [TearDown]
        public void TearDown()
        {
            OutboundRequestSink.ExitRequestScope();
            Patcher.Unpatch();
            _agent?.Dispose();
        }

        [Test]
        public void OnRequest_WhenNoCurrentRequest_Allows()
        {
            OutboundRequestSink.ExitRequestScope();
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(new object(), GetHttpClientSendAsyncMethod(), ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(result, Is.Null);
            });
        }

        [Test]
        public void OnRequest_WhenConnectionHasNoRemoteAddress_Allows()
        {
            EnterRequestScope(new Uri("http://127.0.0.1/admin"), CreateContextWithInput("http://127.0.0.1/admin"));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(new object(), GetHttpClientSendAsyncMethod(), ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(result, Is.Null);
            });
        }

        [Test]
        public void OnRequest_WhenConnectionIsNull_Allows()
        {
            EnterRequestScope(new Uri("http://127.0.0.1/admin"), CreateContextWithInput("http://127.0.0.1/admin"));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(null!, GetHttpClientSendAsyncMethod(), ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(result, Is.Null);
            });
        }

        [Test]
        public void OnRequest_WhenHttp3ConnectionRemoteEndpointBlocks_SetsFaultedResponse()
        {
            var url = "http://127.0.0.1/admin";
            EnterRequestScope(new Uri(url), CreateContextWithInput(url));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(
                new Http3Connection(new RemoteEndpointConnection(IPAddress.Loopback)),
                GetHttpClientSendAsyncMethod(),
                ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.False);
                Assert.That(result, Is.Not.Null);
                Assert.That(async () => await result, Throws.TypeOf<AikidoException>());
            });
        }

        [Test]
        public void OnRequest_WhenHttp3RemoteEndpointIsNotIPEndPoint_Allows()
        {
            EnterRequestScope(new Uri("http://127.0.0.1/admin"), CreateContextWithInput("http://127.0.0.1/admin"));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(
                new Http3Connection(new RemoteEndpointConnection(new DnsEndPoint("localhost", 443))),
                GetHttpClientSendAsyncMethod(),
                ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(result, Is.Null);
            });
        }

        [Test]
        public void OnRequest_WhenSslStreamWrapsSocketStream_Blocks()
        {
            using var pair = new ConnectedSocketPair();
            using var sslStream = new SslStream(new SocketBackedStream(pair.Client));
            var url = $"http://127.0.0.1:{pair.Port}/admin";
            EnterRequestScope(new Uri(url), CreateContextWithInput(url));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(
                new HttpConnection(sslStream),
                GetHttpClientSendAsyncMethod(),
                ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.False);
                Assert.That(result, Is.Not.Null);
                Assert.That(async () => await result, Throws.TypeOf<AikidoException>());
            });
        }

        [Test]
        public void OnRequest_WhenSocketRemoteEndpointThrows_Allows()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Dispose();
            EnterRequestScope(new Uri("http://127.0.0.1/admin"), CreateContextWithInput("http://127.0.0.1/admin"));
            Task<HttpResponseMessage> result = null!;

            var allowed = OutboundRequestInnerSink.OnRequest(
                new Http3Connection(socket),
                GetHttpClientSendAsyncMethod(),
                ref result);

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(result, Is.Null);
            });
        }

        [Test]
        public void OnFrameworkRequest_WhenNoCurrentRequest_Allows()
        {
            OutboundRequestSink.ExitRequestScope();

            var allowed = OutboundRequestInnerSink.OnFrameworkRequest(new object(), GetHttpClientSendAsyncMethod());

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void OnFrameworkRequest_WhenConnectionHasNoNetworkStream_Allows()
        {
            EnterRequestScope(new Uri("http://127.0.0.1/admin"), CreateContextWithInput("http://127.0.0.1/admin"));

            var allowed = OutboundRequestInnerSink.OnFrameworkRequest(new object(), GetHttpClientSendAsyncMethod());

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void OnFrameworkRequest_WhenDetectionAllows_ReturnsTrue()
        {
            using var pair = new ConnectedSocketPair();
            var targetUri = new Uri($"http://backend:{pair.Port}/admin");
            EnterRequestScope(targetUri, CreateContextWithInput("http://unrelated.example/admin"));

            var allowed = OutboundRequestInnerSink.OnFrameworkRequest(
                new FrameworkConnection(new SocketBackedStream(pair.Client)),
                GetHttpClientSendAsyncMethod());

            Assert.That(allowed, Is.True);
        }

        [Test]
        public void OnFrameworkRequest_WhenDetectionBlocks_Rethrows()
        {
            using var pair = new ConnectedSocketPair();
            var url = $"http://127.0.0.1:{pair.Port}/admin";
            EnterRequestScope(new Uri(url), CreateContextWithInput(url));

            Assert.That(
                () => OutboundRequestInnerSink.OnFrameworkRequest(
                    new FrameworkConnection(new SocketBackedStream(pair.Client)),
                    GetHttpClientSendAsyncMethod()),
                Throws.TypeOf<AikidoException>());
        }

        private void EnterRequestScope(Uri targetUri, Context context)
        {
            _activeContext = context;
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

            Assert.That(
                OutboundRequestSink.OnRequestHttpClient(request, httpClient, GetHttpClientSendAsyncMethod()),
                Is.True);
        }

        private static Context CreateContextWithInput(string userInput)
        {
            return new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", userInput }
                }
            };
        }

        private static MethodInfo GetHttpClientSendAsyncMethod()
        {
            return typeof(HttpClient).GetMethod(
                nameof(HttpClient.SendAsync),
                new[] { typeof(HttpRequestMessage), typeof(CancellationToken) })!;
        }

        private sealed class HttpConnection
        {
            private readonly Stream _stream;

            internal HttpConnection(Stream stream)
            {
                _stream = stream;
            }
        }

        private sealed class Http3Connection
        {
            private readonly object _connection;

            internal Http3Connection(object connection)
            {
                _connection = connection;
            }
        }

        private sealed class FrameworkConnection
        {
            internal FrameworkConnection(Stream networkStream)
            {
                NetworkStream = networkStream;
            }

            private Stream NetworkStream { get; }
        }

        private sealed class RemoteEndpointConnection
        {
            internal RemoteEndpointConnection(IPAddress address)
                : this(new IPEndPoint(address, 443))
            {
            }

            internal RemoteEndpointConnection(EndPoint remoteEndPoint)
            {
                RemoteEndPoint = remoteEndPoint;
            }

            private EndPoint RemoteEndPoint { get; }
        }

        private sealed class SocketBackedStream : Stream
        {
            internal SocketBackedStream(Socket socket)
            {
                Socket = socket;
            }

            private Socket Socket { get; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ConnectedSocketPair : IDisposable
        {
            private readonly Socket _accepted;

            internal ConnectedSocketPair()
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;

                Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var connectTask = Client.ConnectAsync(IPAddress.Loopback, Port);
                _accepted = listener.AcceptSocket();
                connectTask.GetAwaiter().GetResult();
            }

            internal int Port { get; }
            internal Socket Client { get; }

            public void Dispose()
            {
                Client.Dispose();
                _accepted.Dispose();
            }
        }
    }
}
