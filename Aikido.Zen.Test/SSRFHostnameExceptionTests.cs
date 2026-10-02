using System.Net;
using System.Runtime.ExceptionServices;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test;

[TestFixture]
[NonParallelizable]
public class SSRFHostnameExceptionTests
{
    [TestCase(64)]
    [TestCase(128)]
    [TestCase(254)]
    public void Detect_WithLongAsciiInput_DoesNotThrowCaughtArgumentExceptions(int length)
    {
        var context = new Context
        {
            Url = "https://service.example/read",
            ParsedUserInput = Enumerable.Range(0, 20).ToDictionary(i => $"query.value{i}", _ => new string('a', length))
        };
        var target = new Uri("https://account.blob.core.windows.net/container/blob");
        var remote = IPAddress.Parse("10.20.30.40");
        var threadId = Environment.CurrentManagedThreadId;
        var exceptions = 0;
        EventHandler<FirstChanceExceptionEventArgs> onException = (_, args) =>
        {
            if (Environment.CurrentManagedThreadId == threadId && args.Exception is ArgumentException)
                exceptions++;
        };
        InspectionResult result;
        AppDomain.CurrentDomain.FirstChanceException += onException;
        try
        {
            result = SSRFDetector.Detect(target, remote, context);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= onException;
        }
        Assert.Multiple(() =>
        {
            Assert.That(result.AttackKind, Is.Null);
            Assert.That(exceptions, Is.Zero);
        });
    }

    [TestCase("localhost", "localhost")]
    [TestCase("\u24DBocalhost", "localhost")]
    [TestCase("b\u00fccher.example", "xn--bcher-kva.example")]
    [TestCase("xn--bcher-kva.example", "b\u00fccher.example")]
    [TestCase("127.0.0.1", "127.0.0.1")]
    [TestCase("[::1]", "[::1]")]
    public void Detect_WithPrivateTargetInUserInput_PreservesSsrfDetection(string userHost, string targetHost)
    {
        var context = new Context
        {
            Url = "https://service.example/read",
            ParsedUserInput = new Dictionary<string, string> { ["query.url"] = $"http://{userHost}/admin" }
        };
        var result = SSRFDetector.Detect(new Uri($"http://{targetHost}/admin"), IPAddress.Loopback, context);
        Assert.That(result.AttackKind, Is.EqualTo(AttackKind.Ssrf));
    }

    [TestCase("xn--", "xn--")]
    [TestCase("XN--BCHER-KVA.EXAMPLE.", "xn--bcher-kva.example")]
    [TestCase("A..B", "a..b")]
    [TestCase("[FD00:EC2::254]", "fd00:ec2::254")]
    public void NormalizeHostname_WithAsciiInput_PreservesExistingResult(string input, string expected)
    {
        Assert.That(SSRFDetector.NormalizeHostname(input), Is.EqualTo(expected));
    }
}
