using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Information about a package manifest.
/// </summary>
public sealed class PackageManifestInfo
{
    /// <summary>
    ///   Unique package identifier.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required Guid PackageID { get; init; }

    /// <summary>
    ///   Package display name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the package does at a high level.
    /// </summary>
    [JsonPropertyName("overview")]
    [JsonProperty("overview")]
    public required string Overview { get; init; }

    /// <summary>
    ///   The author(s) of the package and plugin releases contained within it.
    /// </summary>
    [JsonPropertyName("authors")]
    [JsonProperty("authors")]
    public required string Authors { get; init; }

    /// <summary>
    ///   The repository URL for the package and plugin releases contained
    ///   within it, if provided.
    /// </summary>
    [JsonPropertyName("repository_url")]
    [JsonProperty("repository_url")]
    public required string? RepositoryUrl { get; init; }

    /// <summary>
    ///   The home-page URL for the package and plugin releases contained within
    ///   it, if provided.
    /// </summary>
    [JsonPropertyName("homepage_url")]
    [JsonProperty("homepage_url")]
    public required string? HomepageUrl { get; init; }

    /// <summary>
    ///   Search tags.
    /// </summary>
    [MaxLength(10)]
    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    ///   The thumbnail for the plugin, if it's available.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    [JsonProperty("thumbnail")]
    public required PackageThumbnailInfo? Thumbnail { get; init; }

    /// <summary>
    ///   Available releases from the manifest.
    /// </summary>
    [JsonPropertyName("releases")]
    [JsonProperty("releases")]
    public required IReadOnlyList<PackageReleaseInfo> Releases { get; init; }

    /// <summary>
    ///   When this manifest was last fetched.
    /// </summary>
    [JsonPropertyName("last_fetched_at")]
    [JsonProperty("last_fetched_at")]
    public DateTime LastFetchedAt { get; init; }
}
