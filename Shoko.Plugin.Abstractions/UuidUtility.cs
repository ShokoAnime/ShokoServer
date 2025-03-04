using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Public UUID namespace IDs.
/// <br/>
/// <br/>
/// https://www.rfc-editor.org/rfc/rfc9562.html#section-6.6
/// </summary>
public static class PublicUuidNamespaces
{
    /// <summary>
    /// DNS namespace.
    /// </summary>
    /// <remarks>
    /// Name reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc9499
    /// <br/>
    /// <br/>
    /// Namespace ID Reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc4122
    /// https://datatracker.ietf.org/doc/html/rfc9562
    /// </remarks>
    public static readonly Guid DNS = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// URL namespace.
    /// </summary>
    /// <remarks>
    /// Name reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc1738
    /// <br/>
    /// <br/>
    /// Namespace ID Reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc4122
    /// https://datatracker.ietf.org/doc/html/rfc9562
    /// </remarks>
    public static readonly Guid URL = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// Object ID (X660) namespace.
    /// </summary>
    /// <remarks>
    /// Name reference:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#X660
    /// <br/>
    /// <br/>
    /// Namespace ID Reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc4122
    /// https://datatracker.ietf.org/doc/html/rfc9562
    /// </remarks>
    public static readonly Guid OID = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// X500 namespace.
    /// </summary>
    /// <remarks>
    /// Name reference:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#X500
    /// <br/>
    /// <br/>
    /// Namespace ID Reference:
    /// <br/>
    /// https://datatracker.ietf.org/doc/html/rfc4122
    /// https://datatracker.ietf.org/doc/html/rfc9562
    /// </remarks>
    public static readonly Guid X500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>
    /// Shoko plugins namespace.
    /// </summary>
    public static readonly Guid ShokoPluginAbstractions = "Shoko.Plugin.Abstractions".ToUuidV5(OID);
}

/// <summary>
/// Utility class for generating UUIDs.
/// </summary>
public static class UuidUtility
{
    private static readonly Dictionary<Guid, byte[]> _namespaceIDs = new()
    {
        { PublicUuidNamespaces.DNS, [0x6b, 0xa7, 0xb8, 0x10, 0x9d, 0xad, 0x11, 0xd1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8] },
        { PublicUuidNamespaces.URL, [0x6b, 0xa7, 0xb8, 0x11, 0x9d, 0xad, 0x11, 0xd1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8] },
        { PublicUuidNamespaces.OID, [0x6b, 0xa7, 0xb8, 0x12, 0x9d, 0xad, 0x11, 0xd1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8] },
        { PublicUuidNamespaces.X500, [0x6b, 0xa7, 0xb8, 0x14, 0x9d, 0xad, 0x11, 0xd1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8] },
        { PublicUuidNamespaces.ShokoPluginAbstractions, Convert.FromHexString(PublicUuidNamespaces.ShokoPluginAbstractions.ToString().Replace("-", "")) },
    };

    /// <summary>
    /// Generates a version 3 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 3 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.3
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid ToUuidV3(this string input, Guid namespaceGuid = default)
        => GetV3(input, namespaceGuid);

    /// <summary>
    /// Generates a version 3 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 3 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.3
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid GetV3(string input, Guid namespaceGuid = default)
        => GetV3(System.Text.Encoding.UTF8.GetBytes(input), namespaceGuid);

    /// <summary>
    /// Generates a version 3 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 3 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.3
    /// </remarks>
    /// <param name="input">Input byte array.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid GetV3(byte[] input, Guid namespaceGuid = default)
    {
        if (namespaceGuid == default)
            namespaceGuid = PublicUuidNamespaces.ShokoPluginAbstractions;
        var namespaceBytes = _namespaceIDs.TryGetValue(namespaceGuid, out var globalId)
            ? globalId
            : Convert.FromHexString(namespaceGuid.ToString().Replace("-", ""));
        var bytes = MD5.HashData(namespaceBytes.Concat(input).ToArray());

        // Version and variant
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

        return new Guid(bytes);
    }

    /// <summary>
    /// Generates a version 5 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 5 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.5
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid ToUuidV5(this string input, Guid namespaceGuid = default)
        => GetV5(input, namespaceGuid);

    /// <summary>
    /// Generates a version 5 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 5 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.5
    /// </remarks>
    /// <param name="input">Input text.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid GetV5(string input, Guid namespaceGuid = default)
        => GetV5(System.Text.Encoding.UTF8.GetBytes(input), namespaceGuid);

    /// <summary>
    /// Generates a version 5 UUID from <paramref name="input"/> in the specified namespace.
    /// </summary>
    /// <remarks>
    /// RFC definition of a version 5 UUID:
    /// <br/>
    /// https://www.rfc-editor.org/rfc/rfc9562.html#section-5.5
    /// </remarks>
    /// <param name="input">Input byte array.</param>
    /// <param name="namespaceGuid">UUID namespace to use.</param>
    /// <returns>The new UUID.</returns>
    public static Guid GetV5(byte[] input, Guid namespaceGuid = default)
    {
        if (namespaceGuid == default)
            namespaceGuid = PublicUuidNamespaces.ShokoPluginAbstractions;
        var namespaceBytes = _namespaceIDs.TryGetValue(namespaceGuid, out var globalId)
            ? globalId
            : Convert.FromHexString(namespaceGuid.ToString().Replace("-", ""));
        var bytes = SHA1.HashData(namespaceBytes.Concat(input).ToArray())
            .Take(16)
            .ToArray();

        // Version and variant
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

        return new Guid(bytes);
    }
}
