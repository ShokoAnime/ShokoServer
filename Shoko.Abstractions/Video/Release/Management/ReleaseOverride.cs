using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Video.Release.Management;

/// <summary>
/// A single release group's files for a series (after same-group merge),
/// without the coverage filter applied. Used as the data source for the
/// Mix &amp; Match release override view.
/// </summary>
public class ReleaseOverride
{
    /// <summary>
    /// Stable identifier for this override group, derived from its quality
    /// profile and file composition.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// The release group identifier, if known.
    /// </summary>
    public string? GroupID { get; init; }

    /// <summary>
    /// Where the release group identifier came from, if known.
    /// </summary>
    public string? GroupSource { get; init; }

    /// <summary>
    /// The full name of the release group, if known.
    /// </summary>
    public string? GroupName { get; init; }

    /// <summary>
    /// The short name of the release group, if known.
    /// </summary>
    public string? GroupShortName { get; init; }

    /// <summary>
    /// The source of the release.
    /// </summary>
    public ReleaseSource Source { get; init; }

    /// <summary>
    /// Standard resolution label (e.g. "1080p", "720p", "480p").
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Simplified video codec identifier (e.g. "H264", "HEVC", "AV1").
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Video bit depth (e.g. 8, 10).
    /// </summary>
    public int BitDepth { get; init; }

    /// <summary>
    /// Simplified primary audio codec identifier (e.g. "FLAC", "AAC", "AC3").
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Number of audio tracks in the representative file.
    /// </summary>
    public int AudioStreamCount { get; init; }

    /// <summary>
    /// Number of subtitle/text tracks in the representative file.
    /// </summary>
    public int SubtitleStreamCount { get; init; }

    /// <summary>
    /// Audio languages of the representative file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> AudioLanguages { get; init; } = [];

    /// <summary>
    /// Subtitle languages of the representative file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; } = [];

    /// <summary>
    /// True when this group's files do not cover every known episode.
    /// </summary>
    public bool HasPartialCoverage { get; init; }

    /// <summary>
    /// All files in this override group.
    /// </summary>
    public IReadOnlyList<ReleaseOverrideFile> Files { get; init; } = [];
}

/// <summary>
/// A single file within a <see cref="ReleaseOverride"/>.
/// </summary>
public class ReleaseOverrideFile
{
    /// <summary>
    /// The file location.
    /// </summary>
    public required IVideoFile File { get; init; }

    /// <summary>
    /// Release version (1 = original, 2+ = updated).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// True if the file has chapter markers; null if chapter data is absent.
    /// </summary>
    public bool? IsChaptered { get; init; }

    /// <summary>
    /// Number of subtitle/text tracks in the file.
    /// </summary>
    public int SubtitleStreamCount { get; init; }

    /// <summary>
    /// All (EpisodeType, EpisodeNumber) pairs covered by this file.
    /// </summary>
    public IReadOnlySet<(EpisodeType Type, int Number)> Episodes { get; init; }
        = new HashSet<(EpisodeType, int)>();
}
