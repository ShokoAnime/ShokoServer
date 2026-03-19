using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

#nullable enable
namespace Shoko.Server.Plugin.Models;

/// <summary>
///   Information about a package archive file for download.
/// </summary>
public sealed class RemotePackageArchiveInfo
{
    /// <summary>
    ///   Runtime identifier for the version. Will be <c>"any"</c> for universal
    ///   packages.
    /// </summary>
    [Required]
    [JsonPropertyName("runtime")]
    [JsonProperty("runtime")]
    public string RuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>
    ///   Semantic version for minimum ABI version to run this version.
    /// </summary>
    [Required]
    [JsonPropertyName("abstraction")]
    [JsonProperty("abstraction")]
    public Version AbstractionVersion { get; set; } = new(0, 0, 0);

    /// <summary>
    ///   Download URL for the package's archive.
    /// </summary>
    [Url]
    [Required]
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public string ArchiveUrl { get; set; } = string.Empty;

    /// <summary>
    ///   SHA256 checksum for integrity verification.
    /// </summary>
    [Required]
    [JsonPropertyName("checksum")]
    [JsonProperty("checksum")]
    public string ArchiveChecksum { get; set; } = string.Empty;
}
