using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    /// <summary>
    ///   The archive's packed size, the zip's own, in bytes, or <c>0</c> when
    ///   its manifest does not say. When known, a download of any other size
    ///   is refused.
    /// </summary>
    [JsonPropertyName("packed_size")]
    [JsonProperty("packed_size")]
    public long PackedSize { get; init; }

    /// <summary>
    ///   The size of the archive's files once unpacked, in bytes: the sum of
    ///   their contents, not the disk space they take up. <c>0</c> when its
    ///   manifest does not say. When known, an archive unpacking to any other
    ///   size is refused, and an install needing more space than is free is
    ///   refused before downloading (<see cref="IPluginPackageManager.IsFreeSpaceCheckEnabled"/>).
    /// </summary>
    [JsonPropertyName("unpacked_size")]
    [JsonProperty("unpacked_size")]
    public long UnpackedSize { get; init; }

    /// <summary>
    ///   Free-form data about the archive from its manifest's
    ///   <c>extensions</c>, for frontends and tooling. Passed through as is;
    ///   the server never reads it.
    /// </summary>
    [JsonProperty("extensions", NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore]
    public JObject? Extensions { get; init; }
}
