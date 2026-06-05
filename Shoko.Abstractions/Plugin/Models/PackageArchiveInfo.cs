using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Information about a package archive file for download.
/// </summary>
public sealed class PackageArchiveInfo
{
    /// <summary>
    ///   Runtime identifier for the version. Will be <c>"any"</c> for universal
    ///   packages.
    /// </summary>
    [JsonPropertyName("runtime")]
    [JsonProperty("runtime")]
    public required string RuntimeIdentifier { get; init; }

    /// <summary>
    ///   Semantic version for minimum ABI version required to run this version.
    /// </summary>
    [JsonPropertyName("abstraction")]
    [JsonProperty("abstraction")]
    public required Version AbstractionVersion { get; init; }

    /// <summary>
    ///   Download URL for the package's archive.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public required string ArchiveUrl { get; init; }

    /// <summary>
    ///   Checksum for integrity verification. Defaults to SHA256. Can be
    ///   prefixed with <c>"sha256:"</c>, <c>"sha1:"</c>, or <c>"md5:"</c> to
    ///   use a different hash algorithm.
    /// </summary>
    [JsonPropertyName("checksum")]
    [JsonProperty("checksum")]
    public required string ArchiveChecksum { get; init; }
}
