#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.Settings;

public class ReleaseComparisonPreferences
{
    /// <summary>
    /// Ordered list of quality signals used to rank release candidates.
    /// Comparison stops at the first signal where the two candidates differ.
    /// </summary>
    public List<ReleaseSignalType> SignalPriority { get; set; } =
    [
        ReleaseSignalType.Source,
        ReleaseSignalType.IsCorrupted,
        ReleaseSignalType.Resolution,
        ReleaseSignalType.BitDepth,
        ReleaseSignalType.VideoCodec,
        ReleaseSignalType.Chapters,
        ReleaseSignalType.AudioStreamCount,
        ReleaseSignalType.SubtitleStreamCount,
        ReleaseSignalType.AudioLanguage,
        ReleaseSignalType.SubtitleLanguage,
        ReleaseSignalType.AudioCodec,
        ReleaseSignalType.SubGroup,
        ReleaseSignalType.Version,
        ReleaseSignalType.IsCensored,
    ];

    /// <summary>Ordered source preference: first entry is most preferred.</summary>
    public List<string> SourceOrder { get; set; } = ["BluRay", "DVD", "TV", "Web", "Unknown"];

    /// <summary>Ordered resolution preference: first entry is most preferred.</summary>
    public List<string> ResolutionOrder { get; set; } = ["2160p", "1440p", "1080p", "720p", "480p"];

    /// <summary>Ordered video codec preference: first entry is most preferred.</summary>
    public List<string> VideoCodecOrder { get; set; } = ["HEVC", "H264", "AV1", "MPEG4", "VC1", "MPEG2"];

    /// <summary>Ordered audio codec preference: first entry is most preferred.</summary>
    public List<string> AudioCodecOrder { get; set; } = ["FLAC", "DCA", "AAC", "AC3", "MP3"];

    /// <summary>
    /// Ordered audio language preference: first entry is most preferred.
    /// Candidates are compared against this list in order — the first preferred
    /// language present in one candidate but not the other decides the winner.
    /// Empty list means no language preference — audio language comparison is always a tie.
    /// </summary>
    public List<string> AudioLanguageOrder { get; set; } = [];

    /// <summary>
    /// Ordered subtitle language preference: first entry is most preferred.
    /// Candidates are compared against this list in order — the first preferred
    /// language present in one candidate but not the other decides the winner.
    /// Empty list means no language preference — subtitle language comparison is always a tie.
    /// </summary>
    public List<string> SubtitleLanguageOrder { get; set; } = [];

    /// <summary>
    /// Ordered release-group preference: first entry is most preferred.
    /// Empty list means no group preference — subgroup comparison is always a tie.
    /// </summary>
    public List<string> SubGroupOrder { get; set; } = [];

    /// <summary>When true, 10-bit video is preferred over 8-bit; when false, the opposite.</summary>
    public bool PreferHigherBitDepth { get; set; } = true;

    /// <summary>
    /// When true, the auto-management check runs at the end of every import.
    /// When false, no redundancy check is triggered on import; the check can
    /// still be invoked manually via the API.
    /// </summary>
    public bool AutoDeleteOnImport { get; set; } = false;

    /// <summary>
    /// When true, redundant release candidates are automatically deleted.
    /// When false, the check still runs but only logs what would be removed
    /// (preview/display mode). Requires <see cref="AutoDeleteOnImport"/> to
    /// be true for the deletion to trigger automatically on import.
    /// </summary>
    public bool AllowDeletion { get; set; } = false;

    /// <summary>
    /// When true and a series is still airing, redundancy is evaluated per-file
    /// rather than per-candidate. Individual files whose episode coverage is
    /// already provided by a higher-ranked candidate are deleted, while the
    /// remaining files in that candidate (covering episodes the primary has not
    /// yet reached) are retained.
    ///
    /// When false (or when the series has finished airing), the existing
    /// whole-candidate rule applies: a secondary candidate is only deleted if
    /// its entire episode coverage is already subsumed by the primary.
    /// </summary>
    public bool PerFileDeletionForAiringSeries { get; set; } = true;

    /// <summary>
    /// Controls how episode coverage is measured for mixed-type releases
    /// (releases that contain both regular episodes and specials).
    /// </summary>
    public EpisodeTypeScope EpisodeTypeScope { get; set; } = EpisodeTypeScope.KeepTogether;
}

/// <summary>Signals available for sequential release comparison.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ReleaseSignalType
{
    Source,
    Resolution,
    VideoCodec,
    BitDepth,
    AudioStreamCount,
    SubtitleStreamCount,
    AudioCodec,
    Chapters,
    SubGroup,
    Version,
    IsCorrupted,
    IsCensored,
    /// <summary>
    /// Prefers candidates where all files are from the same release group over
    /// gap-fill candidates that mix files from different release groups.
    /// </summary>
    GroupHomogeneity,
    /// <summary>
    /// Prefers candidates that include audio tracks in the configured preferred
    /// languages. Languages are evaluated in <see cref="ReleaseComparisonPreferences.AudioLanguageOrder"/>
    /// priority; the first preferred language present in one candidate but absent
    /// from the other decides the winner.
    /// </summary>
    AudioLanguage,
    /// <summary>
    /// Prefers candidates that include subtitle tracks in the configured preferred
    /// languages. Languages are evaluated in <see cref="ReleaseComparisonPreferences.SubtitleLanguageOrder"/>
    /// priority; the first preferred language present in one candidate but absent
    /// from the other decides the winner.
    /// </summary>
    SubtitleLanguage,
}

/// <summary>
/// Controls whether releases covering mixed episode types (regular + specials)
/// are treated as a single unit or ranked independently per type.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum EpisodeTypeScope
{
    /// <summary>
    /// Coverage is measured holistically across all episode types.
    /// A specials-only release is never superseded by a regular-episode-only release.
    /// </summary>
    KeepTogether,

    /// <summary>
    /// Coverage is evaluated separately for regular episodes and non-regular episodes.
    /// Allows selecting different releases for regular episodes and specials.
    /// </summary>
    BestPerType,
}
