using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.Plugin.Models;

/// <summary>
/// Information about a package manifest.
/// </summary>
public sealed class RemotePackageManifestInfo
{
    /// <summary>
    /// Unique package identifier.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public Guid PackageID { get; set; }

    /// <summary>
    /// Package display name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Describes what the package does at a high level.
    /// </summary>
    [JsonPropertyName("overview")]
    [JsonProperty("overview")]
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    ///   The author(s) of the package and plugins contained within it.
    /// </summary>
    [JsonPropertyName("authors")]
    [JsonProperty("authors")]
    public string Authors { get; set; } = string.Empty;

    /// <summary>
    ///   The repository URL for the package and plugin releases contained
    ///   within it, if provided.
    /// </summary>
    [JsonPropertyName("repository_url")]
    [JsonProperty("repository_url")]
    public string? RepositoryUrl { get; set; }

    /// <summary>
    ///   The home-page URL for the package and plugin releases contained within
    ///   it, if provided.
    /// </summary>
    [JsonPropertyName("homepage_url")]
    [JsonProperty("homepage_url")]
    public string? HomepageUrl { get; set; }

    /// <summary>
    /// Search tags.
    /// </summary>
    [MaxLength(20)]
    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>
    /// Optional banner URL.
    /// </summary>
    /// <remarks>
    /// Deprecated in favour of <see cref="ThumbnailUrl"/> and
    /// <see cref="IconUrl"/>, which say which of the two a manifest means.
    /// Still read, and still used as the thumbnail when no
    /// <see cref="ThumbnailUrl"/> is given, so existing manifests keep
    /// working.
    /// </remarks>
    [Obsolete("Use ThumbnailUrl or IconUrl instead. Still read as a fallback for ThumbnailUrl.")]
    [JsonPropertyName("image_url")]
    [JsonProperty("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Optional wide image URL, shown where there is room for one.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    [JsonProperty("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Optional square image URL, shown beside the plugin's name where there
    /// is not.
    /// </summary>
    [JsonPropertyName("icon_url")]
    [JsonProperty("icon_url")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// Whether the package is a meta plugin, one that only brings in its
    /// dependencies, or <c>null</c> when the manifest does not say.
    /// </summary>
    [JsonPropertyName("is_meta")]
    [JsonProperty("is_meta")]
    public bool? IsMeta { get; set; }

    /// <summary>
    /// Available releases from the manifest.
    /// </summary>
    [JsonPropertyName("releases")]
    [JsonProperty("releases")]
    public IReadOnlyList<RemotePackageReleaseInfo> Releases { get; set; } = [];

    /// <summary>
    ///   Free-form data about the package, kept as is and never read.
    /// </summary>
    [JsonProperty("extensions")]
    public JObject? Extensions { get; init; }

}
