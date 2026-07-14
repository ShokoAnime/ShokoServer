using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Models.Release;

/// <summary>
/// All files from a single release group (after same-group merge), without the
/// coverage filter applied. Used as the data source for the Mix &amp; Match
/// release override view.
/// </summary>
public class VideoReleaseOverride
{
    /// <summary>
    /// Stable identifier for this override group, derived from its quality profile and
    /// file composition. Unlike <see cref="VideoReleaseCandidate.Key"/>, this is always
    /// unique per distinct file set within a series' override list, since overrides are
    /// single merged-group buckets rather than composites.
    /// </summary>
    public required string Key { get; init; }

    public string? GroupID { get; init; }
    public string? GroupSource { get; init; }
    public string? GroupName { get; init; }
    public string? GroupShortName { get; init; }
    public ReleaseSource Source { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public int BitDepth { get; init; }
    public string? AudioCodec { get; init; }
    public int AudioStreamCount { get; init; }
    public int SubtitleStreamCount { get; init; }
    public IReadOnlyList<TitleLanguage> AudioLanguages { get; init; } = [];
    public IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; } = [];

    /// <summary>True when this group's files do not cover every known episode.</summary>
    public bool HasPartialCoverage { get; init; }

    public IReadOnlyList<VideoReleaseOverrideFile> Files { get; init; } = [];
}

/// <summary>A single file within a <see cref="VideoReleaseOverride"/>.</summary>
public class VideoReleaseOverrideFile
{
    public required VideoLocal_Place Place { get; init; }
    public int Version { get; init; }
    public bool? IsChaptered { get; init; }
    public int SubtitleStreamCount { get; init; }
    public IReadOnlySet<(EpisodeType Type, int Number)> Episodes { get; init; }
        = new HashSet<(EpisodeType, int)>();
}
