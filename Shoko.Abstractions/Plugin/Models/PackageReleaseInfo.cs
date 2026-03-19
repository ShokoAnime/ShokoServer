using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Shoko.Abstractions.Core;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Information about a package release.
/// </summary>
public sealed class PackageReleaseInfo
{
    /// <summary>
    ///   Unique package repository identifier for the release.
    /// </summary>
    [JsonPropertyName("repository_id")]
    [JsonProperty("repository_id")]
    public required Guid RepositoryID { get; init; }

    /// <summary>
    ///   Shared semantic version for all versions of the release. (e.g.,
    ///   "1.2.3").
    /// </summary>
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public required Version Version { get; init; }

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
    [JsonPropertyName("released_at")]
    [JsonProperty("released_at")]
    public required DateTime ReleasedAt { get; init; }

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
    public required string? ReleaseNotes { get; init; }

    /// <summary>
    ///   Available archives for different runtime environments and
    ///   architectures.
    /// </summary>
    [JsonPropertyName("archives")]
    [JsonProperty("archives")]
    public required IReadOnlyList<PackageArchiveInfo> Archives { get; init; }
}
