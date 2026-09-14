using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Video.Release.Management;

/// <summary>
/// A summary of a release candidate identified for a series by the release
/// management system — a set of files estimated to belong to the same release,
/// ranked against the other candidates found for that series.
/// </summary>
public class ReleaseCandidate
{
    /// <summary>
    /// 1-based rank within the series. Rank 1 is the best (primary) candidate.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// SHA-256 fingerprint of this candidate's quality signals. Candidates with
    /// the same key have identical quality profiles.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name built from the candidate's distinguishing
    /// signals: group, resolution, and version strategy. Mixed (gap-fill)
    /// candidates use a "PrimaryGroup + FillerGroup" format.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// True when every file in this candidate has a release info record. When
    /// false, quality signals are derived from MediaInfo only and may be less
    /// accurate.
    /// </summary>
    public bool HasReleaseInfo { get; init; }

    /// <summary>
    /// True when the primary candidate (rank 1) fully covers every episode this
    /// candidate covers, making it safe to delete.
    /// </summary>
    public bool IsRedundant { get; init; }

    /// <summary>
    /// Number of file locations in this candidate that would be deleted by
    /// auto-management given the current ranking.
    /// </summary>
    public int RedundantFileCount { get; init; }

    /// <summary>
    /// Episodes covered by the file locations that would be deleted. Files
    /// shared with the primary candidate are excluded. Empty when nothing would
    /// be deleted.
    /// </summary>
    public IReadOnlySet<(EpisodeType Type, int Number)> RedundantEpisodes { get; init; }
        = new HashSet<(EpisodeType, int)>();

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
    /// Container format (e.g. "Matroska", "MPEG-4").
    /// </summary>
    public string? Container { get; init; }

    /// <summary>
    /// Number of audio tracks in the representative file.
    /// </summary>
    public int AudioStreamCount { get; init; }

    /// <summary>
    /// Number of subtitle/text tracks in the representative file.
    /// </summary>
    public int SubtitleStreamCount { get; init; }

    /// <summary>
    /// True if any file in this candidate has chapter markers; false if chapter
    /// data is present but no file has chapters; null if chapter data is absent
    /// for all files.
    /// </summary>
    public bool? IsChaptered { get; init; }

    /// <summary>
    /// True if any file in this candidate is marked as censored.
    /// </summary>
    public bool? IsCensored { get; init; }

    /// <summary>
    /// Whether the release is creditless (e.g. clean OP/ED), if reported.
    /// </summary>
    public bool? IsCreditless { get; init; }

    /// <summary>
    /// True if any file in this candidate is marked as corrupted.
    /// </summary>
    public bool IsCorrupted { get; init; }

    /// <summary>
    /// Release version (1 = original, 2+ = updated).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// How files were selected when multiple versions of the same episode exist.
    /// </summary>
    public ReleaseVersionStrategy VersionStrategy { get; init; }

    /// <summary>
    /// True when files in this candidate come from more than one release family
    /// (i.e. a gap-fill candidate with an anchor and a filler group).
    /// </summary>
    public bool IsMixed { get; init; }

    /// <summary>
    /// True when all files with a known release group belong to the same group.
    /// </summary>
    public bool IsHomogeneous { get; init; }

    /// <summary>
    /// Short names of secondary contributing groups for gap-fill candidates.
    /// Empty for single-family candidates.
    /// </summary>
    public IReadOnlyList<string> SecondaryGroupNames { get; init; } = [];

    /// <summary>
    /// Audio languages of the representative file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> AudioLanguages { get; init; } = [];

    /// <summary>
    /// Subtitle languages of the representative file.
    /// </summary>
    public IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; } = [];

    /// <summary>
    /// True when this candidate does not cover every episode known to exist
    /// locally for this series.
    /// </summary>
    public bool HasPartialCoverage { get; init; }

    /// <summary>
    /// All (EpisodeType, EpisodeNumber) pairs covered by any file in this candidate.
    /// </summary>
    public IReadOnlySet<(EpisodeType Type, int Number)> EpisodeCoverage { get; init; }
        = new HashSet<(EpisodeType, int)>();

    /// <summary>
    /// All file locations that belong to this release candidate.
    /// </summary>
    public IReadOnlyList<IVideoFile> Files { get; init; } = [];
}
