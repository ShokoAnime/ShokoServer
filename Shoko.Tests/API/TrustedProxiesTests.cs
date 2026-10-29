using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shoko.Server.API;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   The trusted proxies: which entries are read, and that only a listed
///   peer may forward the host a request is for.
/// </summary>
public sealed class TrustedProxiesTests
{
    [Fact]
    public void Parse_ReadsAddressesAndRanges_AndReportsTheRest()
    {
        var networks = TrustedProxies.Parse(["10.0.0.0/8", " 192.168.1.2 ", "fd00::/8", "::1", "", "proxy.local", "10.0.0.0/33"], out var invalid);

        Assert.Equal(
            [
                System.Net.IPNetwork.Parse("10.0.0.0/8"),
                System.Net.IPNetwork.Parse("192.168.1.2/32"),
                System.Net.IPNetwork.Parse("fd00::/8"),
                System.Net.IPNetwork.Parse("::1/128"),
            ],
            networks
        );
        Assert.Equal(["proxy.local", "10.0.0.0/33"], invalid);
    }

    [Theory]
    [InlineData("10.1.2.3", "media.example.com", "https")]
    [InlineData("::ffff:10.1.2.3", "media.example.com", "https")]
    [InlineData("127.0.0.1", "shoko.local:8111", "http")]
    [InlineData("192.168.1.3", "shoko.local:8111", "http")]
    public async Task ForwardedHeaders_AreOnlyTakenFromListedPeers(string peer, string expectedHost, string expectedScheme)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Host = new("shoko.local:8111");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Host"] = "media.example.com";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        var options = TrustedProxies.CreateOptions(TrustedProxies.Parse(["10.0.0.0/8", "192.168.1.2"], out _));

        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);

        Assert.Equal(expectedHost, context.Request.Host.Value);
        Assert.Equal(expectedScheme, context.Request.Scheme);
    }
}
