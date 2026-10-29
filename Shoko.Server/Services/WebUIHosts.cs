using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Shoko.Abstractions.Web;

namespace Shoko.Server.Services;

/// <summary>
///   Reads the hosts of the web UI host overrides and matches requests to
///   them.
/// </summary>
internal static class WebUIHosts
{
    #region Parsing

    /// <summary>
    ///   Reads a host override's host into its key: the host name in lower
    ///   case, with <c>:port</c> when it has one.
    /// </summary>
    /// <param name="value">The host, such as <c>example.com:8111</c> or <c>[::1]</c>.</param>
    /// <param name="key">The key, or an empty string when refused.</param>
    /// <param name="reason">Why the host is refused, or <c>null</c>.</param>
    /// <returns><c>true</c> if the host is well formed.</returns>
    internal static bool TryParse(string? value, out string key, out string? reason)
    {
        key = string.Empty;
        reason = null;
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0)
        {
            reason = "The host is empty.";
            return false;
        }

        string name;
        string? port = null;
        if (trimmed[0] is '[')
        {
            var end = trimmed.IndexOf(']');
            if (end < 0)
            {
                reason = $"\"{trimmed}\" is missing the closing bracket of its IPv6 address.";
                return false;
            }

            name = trimmed[..(end + 1)];
            var rest = trimmed[(end + 1)..];
            if (rest.Length > 0)
            {
                if (rest[0] is not ':')
                {
                    reason = $"\"{trimmed}\" is not a host name or IP address with an optional port.";
                    return false;
                }

                port = rest[1..];
            }
        }
        else
        {
            var colon = trimmed.IndexOf(':');
            if (colon >= 0 && trimmed.IndexOf(':', colon + 1) >= 0)
            {
                reason = $"\"{trimmed}\" has more than one colon; an IPv6 address goes in brackets.";
                return false;
            }

            name = colon < 0 ? trimmed : trimmed[..colon];
            port = colon < 0 ? null : trimmed[(colon + 1)..];
        }

        if (NormalizeName(name) is not { } normalizedName)
        {
            reason = $"\"{name}\" is not a host name or IP address.";
            return false;
        }

        if (port is null)
        {
            key = normalizedName;
            return true;
        }

        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber) || portNumber is < 1 or > 65535)
        {
            reason = $"\"{port}\" is not a port from 1 to 65535.";
            return false;
        }

        key = $"{normalizedName}:{portNumber}";
        return true;
    }

    /// <summary>
    ///   Puts a host name in the form keys use: lower case, and an IPv6
    ///   address in its shortest form, in brackets.
    /// </summary>
    /// <param name="name">The host name, an IPv4 address or a bracketed IPv6 address.</param>
    /// <returns>The normalized name, or <c>null</c> if it is not one.</returns>
    private static string? NormalizeName(string name)
    {
        if (name.Length > 2 && name[0] is '[' && name[^1] is ']')
        {
            return IPAddress.TryParse(name[1..^1], out var address) && address.AddressFamily is AddressFamily.InterNetworkV6
                ? $"[{address}]"
                : null;
        }

        return Uri.CheckHostName(name) is UriHostNameType.Dns or UriHostNameType.IPv4
            ? name.ToLowerInvariant()
            : null;
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Checks a list of host overrides before it is saved.
    /// </summary>
    /// <param name="overrides">The host overrides.</param>
    /// <param name="isKnownProvider">Tells if a web UI with the ID is available.</param>
    /// <returns>The errors, by the index of the entry they are for. Empty when the list is fine.</returns>
    internal static IReadOnlyDictionary<int, string> Validate(IReadOnlyList<WebUIHostOverride> overrides, Func<Guid, bool> isKnownProvider)
    {
        var errors = new Dictionary<int, string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < overrides.Count; index++)
        {
            var entry = overrides[index];
            if (!TryParse(entry.Host, out var key, out var reason))
                errors[index] = reason!;
            else if (!seen.Add(key))
                errors[index] = $"The host \"{entry.Host}\" is listed more than once.";
            else if (!isKnownProvider(entry.ProviderID))
                errors[index] = $"No web UI with ID {entry.ProviderID} is available.";
        }

        return errors;
    }

    #endregion

    #region Matching

    /// <summary>
    ///   Picks what serves a request's host: the entry for its name and port,
    ///   else the one for its name alone, else the default.
    /// </summary>
    /// <typeparam name="T">What the entries map to.</typeparam>
    /// <param name="entries">The entries, by key from <see cref="TryParse"/>.</param>
    /// <param name="host">The request's host.</param>
    /// <param name="fallback">What serves a host without an entry.</param>
    /// <returns>The entry matching the host, or <paramref name="fallback"/>.</returns>
    internal static T Resolve<T>(IReadOnlyDictionary<string, T> entries, HostString host, T fallback)
    {
        if (entries.Count is 0 || !host.HasValue || NormalizeName(host.Host) is not { } name)
            return fallback;

        if (host.Port is { } port && entries.TryGetValue($"{name}:{port}", out var withPort))
            return withPort;

        return entries.TryGetValue(name, out var withoutPort) ? withoutPort : fallback;
    }

    #endregion
}
