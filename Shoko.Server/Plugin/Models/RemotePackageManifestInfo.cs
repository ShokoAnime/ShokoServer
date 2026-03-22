using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using JsonConverter = System.Text.Json.Serialization.JsonConverterAttribute;
using NewtonsoftJsonConverter = Newtonsoft.Json.JsonConverterAttribute;

#nullable enable
namespace Shoko.Server.Plugin.Models;

/// <summary>
/// Information about a package manifest.
/// </summary>
public sealed class RemotePackageManifestInfo
{
    /// <summary>
    /// Manifest type.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public PackageType Type { get; set; } = PackageType.Package;

    /// <summary>
    /// Unique package identifier.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public Guid PackageID { get; set; }

    /// <summary>
    ///   Determines if the manifest is a reference to another manifest.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ManifestUrl))]
    [MemberNotNullWhen(false, nameof(Name))]
    [MemberNotNullWhen(false, nameof(Overview))]
    [MemberNotNullWhen(false, nameof(Authors))]
    [MemberNotNullWhen(false, nameof(Tags))]
    [MemberNotNullWhen(false, nameof(Tags))]
    [MemberNotNullWhen(false, nameof(Releases))]
    public bool IsReference => Type is PackageType.Manifest;

    /// <summary>
    ///   Referenced manifest location. Must be an absolute <c>http://</c>,
    ///   <c>https://</c> or <c>ftp://</c> resource location when set.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public string? ManifestUrl { get; set; }

    /// <summary>
    /// Package display name.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Describes what the package does at a high level.
    /// </summary>
    [JsonPropertyName("overview")]
    [JsonProperty("overview")]
    public string? Overview { get; set; }

    /// <summary>
    ///   The author(s) of the package and plugins contained within it.
    /// </summary>
    [JsonPropertyName("authors")]
    [JsonProperty("authors")]
    public string? Authors { get; set; }

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
    [MaxLength(10)]
    [JsonPropertyName("tags")]
    [JsonProperty("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>
    /// Optional banner URL.
    /// </summary>
    [JsonPropertyName("image_url")]
    [JsonProperty("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Available releases from the manifest.
    /// </summary>
    [JsonPropertyName("releases")]
    [JsonProperty("releases")]
    public IReadOnlyList<RemotePackageReleaseInfo>? Releases { get; set; }
}

/// <summary>
/// Type of package manifest.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[NewtonsoftJsonConverter(typeof(StringEnumConverter))]
public enum PackageType
{
    /// <summary>
    /// A full package definition with all details.
    /// </summary>
    [JsonStringEnumMemberName("package")]
    [DataMember(Name = "package")]
    Package = 0,

    /// <summary>
    /// A reference to a separate manifest file.
    /// </summary>
    [JsonStringEnumMemberName("manifest")]
    [DataMember(Name = "manifest")]
    Manifest = 1,
}
