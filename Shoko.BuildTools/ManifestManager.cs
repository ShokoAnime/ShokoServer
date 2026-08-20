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
    public required Guid ID { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("authors")]
    public string? Authors { get; set; }

    [JsonPropertyName("repository_url")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("homepage_url")]
    public string? HomepageUrl { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///   Top-level dependencies for the plugin. These are injected into the
    ///   built DLL as serialized assembly metadata. Per-release dependencies
    ///   in each release entry are used for install-time resolution.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<ManifestDependency>? Dependencies { get; set; }

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
    public required string Version { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("source_revision")]
    public string? SourceRevision { get; set; }

    [JsonPropertyName("released_at")]
    public DateTime? ReleasedAt { get; set; }

    [JsonPropertyName("channel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReleaseChannel Channel { get; set; } = ReleaseChannel.Stable;

    [JsonPropertyName("release_notes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("dependencies")]
    public List<ManifestDependency>? Dependencies { get; set; }

    [JsonPropertyName("archives")]
    public List<ManifestArchive>? Archives { get; set; }

    /// <inheritdoc cref="Manifest.Extra" />
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ManifestDependency
{
    [JsonPropertyName("id")]
    public required Guid ID { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("optional")]
    public bool IsOptional { get; set; }

    /// <inheritdoc cref="Manifest.Extra" />
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class ManifestArchive
{
    [JsonPropertyName("runtime")]
    public required string Runtime { get; set; }

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
        ReleaseChannel? channel = null,
        string? tag = null,
        string? releaseNotes = null)
    {
        manifest.Releases ??= [];

        var versionStr = $"{version.Major}.{version.Minor}.{version.Build}";
        if (version.Revision > 0)
            versionStr += $".{version.Revision}";
        var abstractionStr = $"{abstractionVersion.Major}.{abstractionVersion.Minor}.{abstractionVersion.Build}";

        // Find existing release for this version
        var release = manifest.Releases.FirstOrDefault(r => r.Version == versionStr);
        if (release is null)
        {
            // Create new release
            release = new ManifestRelease
            {
                Version = versionStr,
                Tag = tag ?? $"v{versionStr}",
                ReleasedAt = DateTime.UtcNow,
                Channel = channel ?? (version.Revision > 0 ? ReleaseChannel.Dev : ReleaseChannel.Stable),
                ReleaseNotes = releaseNotes,
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
        else
        {
            if (channel is not null)
                release.Channel = channel.Value;
            if (tag is not null)
                release.Tag = tag;
            // Only when supplied, so a second runtime joining an existing
            // release does not blank the notes the first one set.
            if (releaseNotes is not null)
                release.ReleaseNotes = releaseNotes;
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
    ///   globally. Returns the dropped releases, newest first.
    /// </summary>
    public static IReadOnlyList<ManifestRelease> PruneReleases(Manifest manifest, int count, string method)
    {
        if (manifest.Releases is null || manifest.Releases.Count <= count)
            return [];

        var kept = method == "global"
            ? manifest.Releases
                .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                .Take(count)
                .ToList()
            : manifest.Releases
                .GroupBy(r => r.Channel)
                .SelectMany(g => g
                    .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                    .Take(count))
                .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
                .ToList();

        var dropped = manifest.Releases
            .Where(r => !kept.Contains(r))
            .OrderByDescending(r => r.ReleasedAt ?? DateTime.MinValue)
            .ToList();
        manifest.Releases = kept;
        return dropped;
    }
}
