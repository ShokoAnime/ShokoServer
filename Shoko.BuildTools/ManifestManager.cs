using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.BuildTools;

/// <summary>
///   JSON model for the plugin manifest (manifest.json).
/// </summary>
internal sealed class Manifest
{
    /// <summary>
    ///   The schema reference an editor validates against. Modelled rather
    ///   than left to <see cref="Extra" /> so it keeps its conventional
    ///   place at the top of the file across a round-trip.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    /// <summary>
    ///   <c>package</c> for a full plugin definition, <c>manifest</c> for a
    ///   reference to another manifest's URL. The schema discriminates on it.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public required Guid ID { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("authors")]
    public string? Authors { get; init; }

    [JsonPropertyName("repository_url")]
    public string? RepositoryUrl { get; init; }

    [JsonPropertyName("homepage_url")]
    public string? HomepageUrl { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }

    /// <summary>
    ///   Top-level dependencies for the plugin. These are injected into the
    ///   built DLL as serialized assembly metadata. Per-release dependencies
    ///   in each release entry are used for install-time resolution.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<ManifestDependency>? Dependencies { get; init; }

    [JsonPropertyName("releases")]
    public List<ManifestRelease>? Releases { get; set; }

    /// <summary>
    ///   Anything in the file this model has no property for, carried
    ///   through a load-and-save unchanged.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ManifestRelease
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    [JsonPropertyName("source_revision")]
    public string? SourceRevision { get; init; }

    [JsonPropertyName("released_at")]
    public DateTime? ReleasedAt { get; init; }

    [JsonPropertyName("channel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReleaseChannel Channel { get; set; } = ReleaseChannel.Stable;

    [JsonPropertyName("release_notes")]
    public string? ReleaseNotes { get; init; }

    [JsonPropertyName("dependencies")]
    public List<ManifestDependency>? Dependencies { get; init; }

    [JsonPropertyName("archives")]
    public List<ManifestArchive>? Archives { get; set; }

    /// <inheritdoc cref="Manifest.Extra" />
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ManifestDependency
{
    [JsonPropertyName("id")]
    public required Guid ID { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("optional")]
    public bool IsOptional { get; init; }

    /// <inheritdoc cref="Manifest.Extra" />
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ManifestArchive
{
    [JsonPropertyName("runtime")]
    public required string Runtime { get; init; }

    [JsonPropertyName("abstraction")]
    public required string Abstraction { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    /// <inheritdoc cref="Manifest.Extra" />
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal enum ReleaseChannel
{
    Stable,
    Dev,
    Debug,
}

/// <summary>
///   Manages reading, writing, and updating plugin manifest.json files.
/// </summary>
internal static class ManifestManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    ///   Load a manifest from a JSON file.
    /// </summary>
    public static Manifest Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Manifest>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to parse manifest.json");
    }

    /// <summary>
    ///   Save a manifest to a JSON file.
    /// </summary>
    public static void Save(Manifest manifest, string path)
    {
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    ///   Add or update a release entry for the given version/ABI/runtime.
    ///   If the release version already exists, adds or updates the
    ///   architecture-specific archive. If the release doesn't exist, creates
    ///   a new release entry.
    /// </summary>
    public static void UpdateOrAddRelease(
        Manifest manifest,
        Version version,
        Version abstractionVersion,
        string runtimeIdentifier,
        IReadOnlyList<DependencyInfo> dependencies,
        string? archiveUrl = null,
        string? checksum = null,
        ReleaseChannel? channel = null)
    {
        manifest.Releases ??= [];

        var versionStr = $"{version.Major}.{version.Minor}.{version.Build}";
        var abstractionStr = $"{abstractionVersion.Major}.{abstractionVersion.Minor}.{abstractionVersion.Build}";

        // Find existing release for this version
        var release = manifest.Releases.FirstOrDefault(r => r.Version == versionStr);
        if (release is null)
        {
            // Create new release
            release = new ManifestRelease
            {
                Version = versionStr,
                Tag = $"v{versionStr}",
                ReleasedAt = DateTime.UtcNow,
                Channel = channel ?? (version.Revision > 0 ? ReleaseChannel.Dev : ReleaseChannel.Stable),
                Archives = [],
                Dependencies = dependencies.Select(d => new ManifestDependency
                {
                    ID = d.PluginID,
                    Version = d.VersionRange,
                    IsOptional = d.IsOptional,
                }).ToList(),
            };
            manifest.Releases.Insert(0, release);
        }
        else if (channel is not null)
        {
            release.Channel = channel.Value;
        }

        // Add or update archive for this runtime
        release.Archives ??= [];
        var existingArchive = release.Archives.FirstOrDefault(a => a.Runtime == runtimeIdentifier);
        if (existingArchive is not null)
        {
            existingArchive.Abstraction = abstractionStr;
            if (archiveUrl is not null)
                existingArchive.Url = archiveUrl;
            if (checksum is not null)
                existingArchive.Checksum = checksum;
        }
        else
        {
            release.Archives.Add(new ManifestArchive
            {
                Runtime = runtimeIdentifier,
                Abstraction = abstractionStr,
                Url = archiveUrl,
                Checksum = checksum,
            });
        }
    }

    /// <summary>
    ///   Prune releases, keeping only the N most recent per channel or
    ///   globally.
    /// </summary>
    public static void PruneReleases(Manifest manifest, int count, string method)
    {
        if (manifest.Releases is null || manifest.Releases.Count <= count)
            return;

        if (method == "global")
        {
            // Keep the N most recent releases regardless of channel
            manifest.Releases = manifest.Releases
                .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                .Take(count)
                .ToList();
        }
        else
        {
            // Per-channel: keep N most recent per channel
            manifest.Releases = manifest.Releases
                .GroupBy(r => r.Channel)
                .SelectMany(g => g
                    .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                    .Take(count))
                .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                .ToList();
        }
    }
}
