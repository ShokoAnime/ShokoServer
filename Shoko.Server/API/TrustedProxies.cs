using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Shoko.Server.API;

/// <summary>
///   Reads the trusted proxies an admin listed into the options of the
///   forwarded headers middleware, which trusts nothing else.
/// </summary>
internal static class TrustedProxies
{
    /// <summary>
    ///   Reads the trusted proxies: IP addresses and CIDR ranges.
    /// </summary>
    /// <param name="entries">The entries, as the admin listed them.</param>
    /// <param name="invalid">The entries that are neither, to warn about.</param>
    /// <returns>The networks of the valid entries, a single address as a network of its own.</returns>
    internal static IReadOnlyList<System.Net.IPNetwork> Parse(IEnumerable<string> entries, out IReadOnlyList<string> invalid)
    {
        var networks = new List<System.Net.IPNetwork>();
        var invalidEntries = new List<string>();
        foreach (var entry in entries)
        {
            var trimmed = entry?.Trim() ?? string.Empty;
            if (trimmed.Length is 0)
                continue;

            if (trimmed.Contains('/'))
            {
                if (System.Net.IPNetwork.TryParse(trimmed, out var network))
                    networks.Add(network);
                else
                    invalidEntries.Add(trimmed);
            }
            else if (IPAddress.TryParse(trimmed, out var address))
            {
                networks.Add(new(address, address.AddressFamily is AddressFamily.InterNetworkV6 ? 128 : 32));
            }
            else
            {
                invalidEntries.Add(trimmed);
            }
        }

        invalid = invalidEntries;
        return networks;
    }

    /// <summary>
    ///   Makes the options trusting the forwarded host, scheme and client
    ///   address from the listed proxies only, through every listed proxy in
    ///   a chain, and from no one by default.
    /// </summary>
    /// <param name="networks">The trusted proxies, from <see cref="Parse"/>.</param>
    /// <returns>The options.</returns>
    internal static ForwardedHeadersOptions CreateOptions(IReadOnlyList<System.Net.IPNetwork> networks)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            // Each entry is only read while the peer it came from is listed, so the chain stops at the first stranger.
            ForwardLimit = null,
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var network in networks)
            options.KnownIPNetworks.Add(network);
        return options;
    }
}
