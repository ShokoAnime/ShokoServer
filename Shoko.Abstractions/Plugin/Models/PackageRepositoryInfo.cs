using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
/// Information about a repository containing one or more package manifests.
/// </summary>
public sealed class PackageRepositoryInfo
{
    /// <summary>
    /// Unique repository identifier based on the URL.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required Guid ID { get; init; }

    /// <summary>
    /// Repository identifier/name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public required string Name { get; init; }

    /// <summary>
    ///   Repository manifest location. Must be an absolute <c>http://</c>,
    ///   <c>https://</c> or <c>ftp://</c> resource location.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public required string Url { get; init; }

    /// <summary>
    ///   Stale time override for this repository. If not set then the default
    ///   stale time is used instead.
    /// </summary>
    [JsonPropertyName("state_time")]
    [JsonProperty("state_time")]
    public required TimeSpan? StaleTime { get; set; }

    /// <summary>
    ///   When this repository was last synced.
    /// </summary>
    [JsonPropertyName("last_fetched_at")]
    [JsonProperty("last_fetched_at")]
    public required DateTime? LastFetchedAt { get; set; }
}
