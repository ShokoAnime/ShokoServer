using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// The primary release chosen for an episode and the reason it was selected.
/// </summary>
public class PrimaryReleaseInfo
{
    /// <summary>
    /// Number of release candidates that cover this episode.
    /// </summary>
    public int CandidateCount { get; init; }

    /// <summary>
    /// Summary of the primary (highest-ranked) release candidate.
    /// </summary>
    public ReleaseCandidateSummary Primary { get; init; } = null!;

    /// <summary>
    /// Why this candidate was selected as the primary.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public PrimaryReleaseReason Reason { get; init; }

    /// <summary>
    /// The signal that decided between the primary and the runner-up.
    /// Null when <see cref="Reason"/> is <see cref="PrimaryReleaseReason.OnlyRelease"/>
    /// or all configured signals were tied.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ReleaseSignalType? DecidingSignal { get; init; }

    /// <summary>
    /// The primary candidate's value for the deciding signal (e.g. <c>"BluRay"</c>, <c>"1080p"</c>).
    /// </summary>
    public string? PrimaryValue { get; init; }

    /// <summary>
    /// The runner-up candidate's value for the deciding signal.
    /// </summary>
    public string? RunnerUpValue { get; init; }
}

/// <summary>Why a release candidate was selected as the primary for an episode.</summary>
public enum PrimaryReleaseReason
{
    /// <summary>Only one release covers this episode — no comparison was needed.</summary>
    OnlyRelease,

    /// <summary>Multiple candidates were compared; this one ranked highest.</summary>
    Ranked,
}

/// <summary>
/// A condensed view of a <see cref="VideoReleaseCandidate"/> for display in
/// <see cref="PrimaryReleaseInfo"/>.
/// </summary>
public class ReleaseCandidateSummary
{
    /// <summary>Quality-signal fingerprint of the candidate.</summary>
    public string Key { get; init; } = string.Empty;

    public string? GroupName { get; init; }

    public string? GroupShortName { get; init; }

    public string Source { get; init; } = string.Empty;

    public string? Resolution { get; init; }

    public string? VideoCodec { get; init; }

    public int BitDepth { get; init; }

    public string? AudioCodec { get; init; }

    public int AudioStreamCount { get; init; }

    public int SubtitleStreamCount { get; init; }

    public bool? IsChaptered { get; init; }

    public bool IsCorrupted { get; init; }

    public bool? IsCensored { get; init; }

    public int Version { get; init; }

    /// <summary>Number of files in this candidate.</summary>
    public int FileCount { get; init; }

    /// <summary>
    /// All episodes covered by this candidate in Shoko episode-string format
    /// (e.g. <c>"1"</c>, <c>"S2"</c>, <c>"C1"</c>).
    /// </summary>
    public IReadOnlyList<string> EpisodeCoverage { get; init; } = [];

    public IReadOnlyList<string> AudioLanguages { get; init; } = [];

    public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];

    public ReleaseCandidateSummary() { }

    public ReleaseCandidateSummary(VideoReleaseCandidate c)
    {
        Key = c.Key;
        GroupName = c.GroupName;
        GroupShortName = c.GroupShortName;
        Source = c.Source.ToString();
        Resolution = c.Resolution;
        VideoCodec = c.VideoCodec;
        BitDepth = c.BitDepth;
        AudioCodec = c.AudioCodec;
        AudioStreamCount = c.AudioStreamCount;
        SubtitleStreamCount = c.SubtitleStreamCount;
        IsChaptered = c.IsChaptered;
        IsCorrupted = c.IsCorrupted;
        IsCensored = c.IsCensored;
        Version = c.Version;
        FileCount = c.Places.Count;
        EpisodeCoverage = c.EpisodeCoverage
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Number)
            .Select(EpisodeKeyToString)
            .ToList();
        AudioLanguages = c.AudioLanguages.Select(l => l.GetString()).ToList();
        SubtitleLanguages = c.SubtitleLanguages.Select(l => l.GetString()).ToList();
    }

    private static string EpisodeKeyToString((EpisodeType Type, int Number) key)
        => key.Type.Prefix + key.Number;
}
