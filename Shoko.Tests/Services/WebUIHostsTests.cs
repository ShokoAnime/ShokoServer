using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.Web;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   The web UI host overrides: which entry serves a request's host, and
///   which lists may be saved.
/// </summary>
public sealed class WebUIHostsTests
{
    private static readonly Guid _known = Guid.Parse("11111111-1111-1111-1111-111111111111");

    #region Matching

    [Theory]
    [InlineData("media.example.com:8111", "port")]
    [InlineData("MEDIA.Example.com:8111", "port")]
    [InlineData("media.example.com:9000", "name")]
    [InlineData("media.example.com", "name")]
    [InlineData("other.example.com:8111", "default")]
    [InlineData("[::1]:8111", "ipv6")]
    [InlineData("[0:0::1]:8111", "ipv6")]
    [InlineData("", "default")]
    public void Resolve_PrefersPortThenNameThenDefault(string requestHost, string expected)
    {
        var entries = Entries("media.example.com:8111", "port", "Media.Example.com", "name", "[::1]", "ipv6");

        Assert.Equal(expected, WebUIHosts.Resolve(entries, new HostString(requestHost), "default"));
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData("example.com")]
    [InlineData("localhost:8111")]
    [InlineData("192.168.1.10:65535")]
    [InlineData("[fe80::1]:1")]
    public void Validate_AcceptsWellFormedHosts(string host)
        => Assert.Empty(WebUIHosts.Validate([new() { Host = host, ProviderID = _known }], id => id == _known));

    [Theory]
    [InlineData("")]
    [InlineData("example.com:0")]
    [InlineData("example.com:65536")]
    [InlineData("example.com:http")]
    [InlineData("example.com:")]
    [InlineData("::1")]
    [InlineData("[::1")]
    [InlineData("[example.com]")]
    [InlineData("http://example.com")]
    [InlineData("exa mple.com")]
    public void Validate_RefusesMalformedHosts(string host)
        => Assert.Equal([0], WebUIHosts.Validate([new() { Host = host, ProviderID = _known }], id => id == _known).Keys);

    [Fact]
    public void Validate_RefusesDuplicatesAndUnknownWebUIs()
    {
        WebUIHostOverride[] overrides =
        [
            new() { Host = "example.com", ProviderID = _known },
            new() { Host = "EXAMPLE.com", ProviderID = _known },
            new() { Host = "example.com:8111", ProviderID = _known },
            new() { Host = "other.example.com", ProviderID = Guid.NewGuid() },
        ];

        Assert.Equal([1, 3], WebUIHosts.Validate(overrides, id => id == _known).Keys);
    }

    #endregion

    private static Dictionary<string, string> Entries(params string[] hostsAndValues)
    {
        var entries = new Dictionary<string, string>();
        for (var i = 0; i < hostsAndValues.Length; i += 2)
        {
            Assert.True(WebUIHosts.TryParse(hostsAndValues[i], out var key, out _));
            entries[key] = hostsAndValues[i + 1];
        }

        return entries;
    }
}
