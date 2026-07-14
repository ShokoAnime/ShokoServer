using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Models.Release;

/// <summary>
/// A set of <see cref="VideoLocal_Place"/> files estimated to belong to the
/// same release, as determined by <c>VideoReleaseGroupingService</c>.
/// </summary>
/// <remarks>
/// The <see cref="Key"/> is a SHA-256 hash of the quality signals (group,
/// source, languages, codec, resolution, stream counts, flags, version). For
/// non-mixed (single, homogeneous) candidates it does not encode folder
/// location, so two pure candidates with identical quality profiles produce
/// the same key regardless of where the files live — they represent the same
/// kind of release. For mixed (<see cref="IsMixed"/>) gap-fill composites, the
/// key additionally folds in the underlying file/place composition, since two
/// different combinations of releases stitched together to reach full episode
/// coverage can otherwise majority-vote to an identical aggregate profile
/// while covering the same episodes with genuinely different files.
/// </remarks>
public class VideoReleaseCandidate
{
    /// <summary>
    /// SHA-256 hex fingerprint of this candidate. For non-mixed candidates,
    /// identical keys mean identical quality profiles. For mixed candidates,
    /// the key is additionally unique per underlying file composition.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// True when every place in this candidate has a provider-backed
    /// <see cref="StoredReleaseInfo"/> record.
    /// </summary>
    public bool HasReleaseInfo { get; init; }

    // Release group metadata (from StoredReleaseInfo; null when HasReleaseInfo is false)
    public string? GroupID { get; init; }

    public string? GroupSource { get; init; }

    public string? GroupName { get; init; }

    public string? GroupShortName { get; init; }

    public ReleaseSource Source { get; init; }

    public IReadOnlyList<TitleLanguage> AudioLanguages { get; init; } = [];

    public IReadOnlyList<TitleLanguage> SubtitleLanguages { get; init; } = [];

    // MediaInfo stream signals (from the representative file in the group)

    /// <summary>
    /// Simplified video codec identifier (e.g. "H264", "HEVC", "AV1").
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Video bit depth (e.g. 8, 10).
    /// </summary>
    public int BitDepth { get; init; }

    /// <summary>
    /// Standard resolution label (e.g. "1080p", "720p", "480p").
    /// </summary>
    public string? Resolution { get; init; }

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
    /// Aggregated across all files — individual files within a release can differ.
    /// </summary>
    public bool? IsChaptered { get; init; }

    /// <summary>
    /// True if any file in this candidate is marked as censored.
    /// Aggregated across all files — individual files within a release can differ.
    /// </summary>
    public bool? IsCensored { get; init; }

    /// <summary>
    /// Whether the release is creditless (e.g. clean OP/ED), if reported.
    /// </summary>
    public bool? IsCreditless { get; init; }

    /// <summary>
    /// True if any file in this candidate is marked as corrupted.
    /// Aggregated across all files — individual files within a release can differ.
    /// </summary>
    public bool IsCorrupted { get; init; }

    /// <summary>
    /// Release version from the provider (1 = original, 2+ = updated).
    /// For <see cref="ReleaseVersionStrategy.BestAvailable"/>, this is the
    /// maximum version across all files. For
    /// <see cref="ReleaseVersionStrategy.Consistent"/>, this is the target version.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// How files were selected when multiple versions of the same episode exist.
    /// </summary>
    public ReleaseVersionStrategy VersionStrategy { get; init; }

    /// <summary>
    /// True when files in this candidate come from more than one release family
    /// (i.e., a gap-fill candidate with an anchor and a filler group).
    /// </summary>
    public bool IsMixed { get; init; }

    /// <summary>
    /// True when all files with a known release group belong to the same group.
    /// Single-family candidates are always homogeneous. A gap-fill candidate is
    /// homogeneous when the anchor and filler share the same release group (e.g.,
    /// two ToonsHub buckets that differ only in subtitle language registration).
    /// </summary>
    public bool IsHomogeneous { get; init; }

    /// <summary>
    /// Short names of secondary contributing groups for gap-fill candidates.
    /// Empty for single-family candidates.
    /// </summary>
    public IReadOnlyList<string> SecondaryGroupNames { get; init; } = [];

    /// <summary>
    /// True when files in this candidate disagree on chapter status.
    /// </summary>
    public bool IsChapteredMixed { get; init; }

    /// <summary>
    /// True when files in this candidate disagree on censor status.
    /// </summary>
    public bool IsCensoredMixed { get; init; }

    /// <summary>
    /// True when files in this candidate disagree on creditless status.
    /// </summary>
    public bool IsCreditlessMixed { get; init; }

    /// <summary>
    /// All file locations that belong to this release candidate.
    /// </summary>
    public IReadOnlyList<VideoLocal_Place> Places { get; init; } = [];

    /// <summary>
    /// Per-file quality signals keyed by <see cref="VideoLocal_Place.ID"/>.
    /// Populated for every place in <see cref="Places"/>.
    /// </summary>
    public IReadOnlyDictionary<int, PlaceQualitySignals> PlaceSignals { get; init; }
        = new Dictionary<int, PlaceQualitySignals>();

    /// <summary>
    /// All (EpisodeType, EpisodeNumber) pairs covered by any file in this candidate.
    /// Empty when no file has cross-reference data. Cross-references to a different
    /// anime (crossover/tie-in releases) and cross-references whose episode metadata
    /// isn't cached locally are excluded — see
    /// <c>VideoReleaseGroupingService.GetEpisodeCoverageForAnime</c>.
    /// </summary>
    public IReadOnlySet<(EpisodeType Type, int Number)> EpisodeCoverage { get; init; }
        = new HashSet<(EpisodeType, int)>();

    /// <summary>
    /// Maps each covered (EpisodeType, EpisodeNumber) to the short name of the
    /// release group that provides that episode in this candidate. Null values mean
    /// the file has no <see cref="StoredReleaseInfo"/> (manually linked or unrecognised).
    /// For pure candidates every episode maps to the same group; for gap-fill candidates
    /// anchor and filler episodes map to different groups.
    /// </summary>
    public IReadOnlyDictionary<(EpisodeType, int), string?> EpisodeGroupMap { get; init; }
        = new Dictionary<(EpisodeType, int), string?>();

    /// <summary>
    /// Aggregated quality signals per <see cref="EpisodeType"/>. Populated for every
    /// episode type covered by at least one file in this candidate. Used by
    /// <c>ReleaseComparisonService</c> under <c>EpisodeTypeScope.BestPerType</c> to
    /// compare source quality, resolution, etc. independently for regular episodes,
    /// specials, credits, and so on.
    /// </summary>
    public IReadOnlyDictionary<EpisodeType, EpisodeTypeQualitySignals> TypeSignals { get; init; }
        = new Dictionary<EpisodeType, EpisodeTypeQualitySignals>();

    /// <summary>
    /// True when this candidate does not cover every episode known to exist
    /// locally for this series. Partial candidates are still returned so the
    /// user can see all available release options; this flag lets the UI warn
    /// that choosing this candidate would leave some episodes without a file.
    /// </summary>
    public bool HasPartialCoverage { get; init; }
}

/// <summary>Per-file quality signals for a single <see cref="VideoLocal_Place"/>.</summary>
public record PlaceQualitySignals(
    bool? IsChaptered,
    bool? IsCensored,
    bool? IsCreditless,
    bool IsCorrupted,
    IReadOnlyList<TitleLanguage> AudioLanguages,
    IReadOnlyList<TitleLanguage> SubtitleLanguages,
    ReleaseSource? Source,
    string? Resolution,
    string? VideoCodec,
    int BitDepth,
    string? AudioCodec,
    int AudioStreamCount,
    int SubtitleStreamCount,
    int Version);

/// <summary>
/// Aggregated quality signals for all files in a candidate that cover a
/// particular <see cref="EpisodeType"/>. Used by
/// <c>ReleaseComparisonService</c> when <c>EpisodeTypeScope.BestPerType</c>
/// is active so that specials, credits, etc. can be ranked independently from
/// regular episodes.
/// </summary>
public record EpisodeTypeQualitySignals(
    ReleaseSource Source,
    string? Resolution,
    string? VideoCodec,
    int BitDepth,
    string? AudioCodec,
    int AudioStreamCount,
    int SubtitleStreamCount,
    bool? IsChaptered,
    bool? IsCensored,
    bool? IsCreditless,
    bool IsCorrupted,
    IReadOnlyList<TitleLanguage> AudioLanguages,
    IReadOnlyList<TitleLanguage> SubtitleLanguages);
