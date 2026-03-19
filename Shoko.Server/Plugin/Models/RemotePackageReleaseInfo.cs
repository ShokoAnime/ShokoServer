using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Shoko.Abstractions.Core;

#nullable enable
namespace Shoko.Server.Plugin.Models;

/// <summary>
///   Information about a package release.
/// </summary>
public sealed class RemotePackageReleaseInfo
{
    /// <summary>
    ///   Shared semantic version for all versions of the release. (e.g.,
    ///   "1.2.3").
    /// </summary>
    [Required]
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public Version Version { get; set; } = new(0, 0, 0);

    /// <summary>
    ///   The source revision tag of the release, if provided.
    /// </summary>
    [JsonPropertyName("tag")]
    [JsonProperty("tag")]
    public required string? Tag { get; init; }

    /// <summary>
    ///   The SHA digest of the source revision used to build the release, if
    ///   provided.
    /// </summary>
    [JsonPropertyName("source_revision")]
    [JsonProperty("source_revision")]
    public required string? SourceRevision { get; init; }

    /// <summary>
    ///   When the release was made.
    /// </summary>
    [Required]
    [JsonPropertyName("released_at")]
    [JsonProperty("released_at")]
    public DateTime ReleasedAt { get; set; } = DateTime.UnixEpoch;

    /// <summary>
    ///   The channel for the release.
    /// </summary>
    [JsonPropertyName("channel")]
    [JsonProperty("channel")]
    public required ReleaseChannel Channel { get; init; }

    /// <summary>
    ///   Release notes, or <see langword="null"/> if not available for this
    ///   release.
    /// </summary>
    [JsonPropertyName("release_notes")]
    [JsonProperty("release_notes")]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    ///   Available archives for different runtime environments and
    ///   architectures.
    /// </summary>
    [Required]
    [JsonPropertyName("archives")]
    [JsonProperty("archives")]
    public IReadOnlyList<RemotePackageArchiveInfo> Archives { get; init; } = [];
}
