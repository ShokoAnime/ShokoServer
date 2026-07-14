using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// A ranked release candidate for a series, with full quality-signal and
/// file details.
/// </summary>
public class ReleaseCandidate
{
    /// <summary>
    /// 1-based rank within the series. Rank 1 is the best (primary) candidate.
    /// </summary>
    [Required]
    public required int Rank { get; init; }

    /// <summary>
    /// SHA-256 fingerprint of this candidate's quality signals. Candidates with
    /// the same key have identical quality profiles.
    /// </summary>
    [Required]
    public required string Key { get; init; }

    /// <summary>
    /// Human-readable display name built from the candidate's distinguishing
    /// signals: group, resolution, and version strategy. Mixed (gap-fill)
    /// candidates use a "PrimaryGroup + FillerGroup" format.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// True when every file in this candidate has a <c>StoredReleaseInfo</c>
    /// record. When false, quality signals are derived from MediaInfo only and
    /// may be less accurate.
    /// </summary>
    [Required]
    public required bool HasReleaseInfo { get; init; }

    /// <summary>
    /// True when the primary candidate (rank 1) fully covers every episode this
    /// candidate covers, making it safe to delete.
    /// </summary>
    [Required]
    public required bool IsRedundant { get; init; }

    /// <summary>
    /// Number of file locations in this candidate that would be deleted by
    /// auto-management given the current ranking. For non-airing series this
    /// equals <see cref="Files"/>.Count when <see cref="IsRedundant"/> is true
    /// and 0 otherwise. For airing series (per-file deletion) it may be any
    /// value from 0 to <see cref="Files"/>.Count.
    /// </summary>
    [Required]
    public required int RedundantFileCount { get; init; }

    /// <summary>
    /// Episodes covered by the file locations that would be deleted. Files
    /// shared with the primary candidate are excluded. Empty when nothing
    /// would be deleted.
    /// </summary>
    [Required]
    public required IReadOnlyList<EpisodeCoverage> RedundantEpisodes { get; init; }

    /// <summary>
    /// The quality signal that caused this candidate to rank below the previous
    /// one in the ordered list. Null for the primary (rank 1) candidate.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ReleaseSignalType? DecidingSignal { get; init; }

    /// <summary>
    /// The episode type for which <see cref="DecidingSignal"/> was evaluated when
    /// <c>EpisodeTypeScope.BestPerType</c> is active. Null for rank-1 candidates,
    /// for <c>KeepTogether</c> comparisons, and for signals with no per-type
    /// representation (SubGroup, Version).
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public EpisodeType? DecidingType { get; init; }

    /// <summary>
    /// The display value of <see cref="DecidingSignal"/> for the higher-ranked
    /// (winning) candidate. Null when <see cref="DecidingSignal"/> is null.
    /// </summary>
    public string? WinnerValue { get; init; }

    /// <summary>
    /// The display value of <see cref="DecidingSignal"/> for this candidate
    /// (the loser). Null when <see cref="DecidingSignal"/> is null.
    /// </summary>
    public string? LoserValue { get; init; }

    // ── Release group ───────────────────────────────────────────────────────

    public string? GroupID { get; init; }

    public string? GroupName { get; init; }

    public string? GroupShortName { get; init; }

    // ── Quality signals ─────────────────────────────────────────────────────

    public string? Source { get; init; }

    public string? Resolution { get; init; }

    public string? VideoCodec { get; init; }

    public int BitDepth { get; init; }

    public string? AudioCodec { get; init; }

    public int AudioStreamCount { get; init; }

    public int SubtitleStreamCount { get; init; }

    public bool? IsChaptered { get; init; }

    public bool? IsCensored { get; init; }

    public bool? IsCreditless { get; init; }

    public bool IsCorrupted { get; init; }

    public int Version { get; init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ReleaseVersionStrategy VersionStrategy { get; init; }

    public bool IsMixed { get; init; }

    public bool IsHomogeneous { get; init; }

    public IReadOnlyList<string> SecondaryGroupNames { get; init; } = [];

    public bool IsChapteredMixed { get; init; }

    public bool IsCensoredMixed { get; init; }

    public bool IsCreditlessMixed { get; init; }

    public IReadOnlyList<string> AudioLanguages { get; init; } = [];

    public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];

    // ── Files & coverage ────────────────────────────────────────────────────

    /// <summary>
    /// All file locations that belong to this release candidate.
    /// </summary>
    [Required]
    public required IReadOnlyList<File> Files { get; init; }

    /// <summary>
    /// All (EpisodeType, EpisodeNumber) pairs covered by any file in this candidate.
    /// </summary>
    [Required]
    public required IReadOnlyList<EpisodeCoverage> Episodes { get; init; }

    // ── Factory ─────────────────────────────────────────────────────────────

    public static ReleaseCandidate FromCandidate(
        VideoReleaseCandidate candidate,
        int rank,
        bool isRedundant,
        Dictionary<int, VideoLocal> videoLookup,
        HashSet<int>? redundantPlaceIDs = null,
        ReleaseComparisonService.CompareDecision? decision = null,
        Dictionary<int, IReadOnlySet<(EpisodeType, int)>>? placeEpisodeCoverage = null,
        bool includeResolution = true,
        bool includeSource = true,
        bool includeVersion = true,
        string? nameOverride = null)
    {
        var files = candidate.Places
            .Select(place =>
            {
                videoLookup.TryGetValue(place.VideoID, out var video);
                var episodes = placeEpisodeCoverage?.TryGetValue(place.ID, out var cov) == true
                    ? cov.Select(e => new EpisodeCoverage { Type = e.Item1, Number = e.Item2 })
                          .OrderBy(e => e.Type).ThenBy(e => e.Number)
                          .ToList()
                    : (IReadOnlyList<EpisodeCoverage>)[];
                candidate.PlaceSignals.TryGetValue(place.ID, out var signals);
                return new File
                {
                    PlaceID = place.ID,
                    VideoLocalID = place.VideoID,
                    AbsolutePath = place.Path,
                    FileSize = video?.FileSize ?? 0,
                    IsRedundant = redundantPlaceIDs?.Contains(place.ID) ?? isRedundant,
                    IsChaptered = signals?.IsChaptered,
                    IsCensored = signals?.IsCensored,
                    IsCreditless = signals?.IsCreditless,
                    IsCorrupted = signals?.IsCorrupted ?? false,
                    Source = signals?.Source switch
                    {
                        ReleaseSource.BluRay => "BluRay",
                        ReleaseSource.DVD => "DVD",
                        ReleaseSource.TV => "TV",
                        ReleaseSource.Web => "Web",
                        ReleaseSource.VHS => "VHS",
                        ReleaseSource.VCD => "VCD",
                        ReleaseSource.LaserDisc => "LaserDisc",
                        ReleaseSource.Camera => "Camera",
                        ReleaseSource.Film => "Film",
                        _ => null,
                    },
                    Resolution = signals?.Resolution,
                    VideoCodec = signals?.VideoCodec,
                    BitDepth = signals?.BitDepth ?? 0,
                    AudioCodec = signals?.AudioCodec,
                    AudioStreamCount = signals?.AudioStreamCount ?? 0,
                    SubtitleStreamCount = signals?.SubtitleStreamCount ?? 0,
                    Version = signals?.Version ?? 1,
                    AudioLanguages = signals?.AudioLanguages.Select(l => l.ToString()).ToList() ?? [],
                    SubtitleLanguages = signals?.SubtitleLanguages.Select(l => l.ToString()).ToList() ?? [],
                    Episodes = episodes,
                };
            })
            .OrderBy(f => f.Episodes.Count > 0 ? (int)f.Episodes[0].Type : int.MaxValue)
            .ThenBy(f => f.Episodes.Count > 0 ? f.Episodes[0].Number : int.MaxValue)
            .ToList();

        var episodes = candidate.EpisodeCoverage
            .Select(e =>
            {
                candidate.EpisodeGroupMap.TryGetValue(e, out var groupShortName);
                return new EpisodeCoverage { Type = e.Type, Number = e.Number, GroupShortName = groupShortName };
            })
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Number)
            .ToList();

        var source = candidate.Source switch
        {
            ReleaseSource.BluRay => "BluRay",
            ReleaseSource.DVD => "DVD",
            ReleaseSource.TV => "TV",
            ReleaseSource.Web => "Web",
            ReleaseSource.VHS => "VHS",
            ReleaseSource.VCD => "VCD",
            ReleaseSource.LaserDisc => "LaserDisc",
            ReleaseSource.Camera => "Camera",
            ReleaseSource.Film => "Film",
            _ => null,
        };

        var name = nameOverride ?? BuildName(candidate, source, episodes,
            includeResolution, includeSource, includeVersion);

        return new ReleaseCandidate
        {
            Rank = rank,
            Key = candidate.Key,
            Name = name,
            HasReleaseInfo = candidate.HasReleaseInfo,
            IsRedundant = isRedundant,
            RedundantFileCount = files.Count(f => f.IsRedundant),
            RedundantEpisodes = files
                .Where(f => f.IsRedundant)
                .SelectMany(f => f.Episodes)
                .DistinctBy(e => (e.Type, e.Number))
                .OrderBy(e => e.Type).ThenBy(e => e.Number)
                .ToList(),
            DecidingSignal = decision?.DecidingSignal,
            DecidingType = decision?.DecidingType,
            WinnerValue = decision?.PrimaryValue,
            LoserValue = decision?.RunnerUpValue,
            GroupID = candidate.GroupID,
            GroupName = candidate.GroupName,
            GroupShortName = candidate.GroupShortName,
            Source = source,
            Resolution = candidate.Resolution,
            VideoCodec = candidate.VideoCodec,
            BitDepth = candidate.BitDepth,
            AudioCodec = candidate.AudioCodec,
            AudioStreamCount = candidate.AudioStreamCount,
            SubtitleStreamCount = candidate.SubtitleStreamCount,
            IsChaptered = candidate.IsChaptered,
            IsCensored = candidate.IsCensored,
            IsCreditless = candidate.IsCreditless,
            IsCorrupted = candidate.IsCorrupted,
            Version = candidate.Version,
            VersionStrategy = candidate.VersionStrategy,
            IsMixed = candidate.IsMixed,
            IsHomogeneous = candidate.IsHomogeneous,
            SecondaryGroupNames = candidate.SecondaryGroupNames,
            IsChapteredMixed = candidate.IsChapteredMixed,
            IsCensoredMixed = candidate.IsCensoredMixed,
            IsCreditlessMixed = candidate.IsCreditlessMixed,
            AudioLanguages = candidate.AudioLanguages.Select(l => l.ToString()).ToList(),
            SubtitleLanguages = candidate.SubtitleLanguages.Select(l => l.ToString()).ToList(),
            Files = files,
            Episodes = episodes,
        };
    }

    public static string BuildName(
        VideoReleaseCandidate candidate, string? source,
        IReadOnlyList<EpisodeCoverage> resolvedEpisodes,
        bool includeResolution, bool includeSource, bool includeVersion)
    {
        // Use per-episode breakdown for mixed (gap-fill) candidates AND for anonymous
        // candidates where the EpisodeGroupMap reveals named-group diversity — e.g. a
        // named-group file that landed in an anonymous bucket because its episode did not
        // overlap with the bucket's other files.  This is "better info" than a generic
        // quality-signal label such as "Web Multi-Audio".
        var usePerEpisodeName = candidate.IsMixed
            || (candidate.GroupShortName is null && resolvedEpisodes.Any(e => e.GroupShortName is not null));

        if (usePerEpisodeName)
        {
            // Compute a quality-enriched label for ungrouped (anonymous) episodes.
            // When the anonymous bucket is the candidate's anchor (GroupShortName is null),
            // the candidate-level quality signals describe those exact files directly, so we
            // append them as qualifiers.  When the anchor is a named group the anonymous
            // files are fillers and we fall back to plain "Unknown" to avoid attributing the
            // anchor's signals to a different file set.
            string unknownLabel;
            if (candidate.GroupShortName is null)
            {
                // Only include signals that deviate from the common anime baseline so that
                // the label is informative rather than repetitive noise.
                var qualifiers = new List<string>();
                // Source: "Web" is the expected default — only flag distinctly different sources.
                if (source is not null && source != "Web") qualifiers.Add(source);
                // Resolution: "1080p" is the expected default — flag anything that differs.
                if (includeResolution && candidate.Resolution is not null && candidate.Resolution != "1080p") qualifiers.Add(candidate.Resolution);
                // Codec: "H264" is the expected default — flag notably different codecs (HEVC, AV1, …).
                if (candidate.VideoCodec is not null && candidate.VideoCodec != "H264") qualifiers.Add(candidate.VideoCodec);
                // 10-bit is always worth calling out; 8-bit is the expected default.
                if (candidate.BitDepth == 10) qualifiers.Add("10-bit");
                // Multi-audio is always distinctive.
                if (candidate.AudioStreamCount > 1) qualifiers.Add("Multi-Audio");
                unknownLabel = qualifiers.Count > 0 ? "Unknown " + string.Join(" ", qualifiers) : "Unknown";
            }
            else
            {
                unknownLabel = "Unknown";
            }

            // Show each contributing group with its episode range:
            // "Erai-raws (1-7) + DRiFTKiNG (8-12)".
            // Groups are ordered by the first episode number they provide.
            var segments = resolvedEpisodes
                .Where(e => e.Type == EpisodeType.Episode)
                .GroupBy(e => e.GroupShortName ?? unknownLabel)
                .OrderBy(g => g.Min(e => e.Number))
                .Select(g => $"{g.Key} ({FormatEpisodeRange(g.Select(e => e.Number))})")
                .ToList();

            // Fall back to the flat list when episode data isn't available.
            if (segments.Count == 0)
            {
                var primary = candidate.GroupName ?? candidate.GroupShortName ?? "Unknown";
                var all = new[] { primary }.Concat(candidate.SecondaryGroupNames.Where(s => !string.IsNullOrEmpty(s)));
                return string.Join(" + ", all);
            }

            return string.Join(" + ", segments);
        }

        var parts = new List<string>();
        var groupLabel = candidate.GroupName ?? candidate.GroupShortName;

        if (groupLabel is not null)
        {
            parts.Add(groupLabel);
            if (includeResolution && candidate.Resolution is not null) parts.Add(candidate.Resolution);
            if (includeSource && source is not null) parts.Add(source);
        }
        else if (source is not null)
        {
            parts.Add(source);
            if (includeResolution && candidate.Resolution is not null) parts.Add(candidate.Resolution);
        }
        else if (candidate.Resolution is not null)
        {
            parts.Add(candidate.Resolution);
        }
        else
        {
            parts.Add("Unknown");
        }

        if (includeVersion && candidate.VersionStrategy == ReleaseVersionStrategy.Consistent)
            parts.Add($"v{candidate.Version}");

        if (candidate.AudioStreamCount > 1)
            parts.Add("Multi-Audio");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Computes the display name for a candidate without building the full DTO.
    /// Used by callers that need to detect name collisions before constructing DTOs.
    /// </summary>
    public static string ComputeName(
        VideoReleaseCandidate candidate,
        bool includeResolution, bool includeSource, bool includeVersion)
    {
        var source = candidate.Source switch
        {
            ReleaseSource.BluRay => "BluRay",
            ReleaseSource.DVD => "DVD",
            ReleaseSource.TV => "TV",
            ReleaseSource.Web => "Web",
            ReleaseSource.VHS => "VHS",
            ReleaseSource.VCD => "VCD",
            ReleaseSource.LaserDisc => "LaserDisc",
            ReleaseSource.Camera => "Camera",
            ReleaseSource.Film => "Film",
            _ => null,
        };

        var episodes = candidate.EpisodeCoverage
            .Select(e =>
            {
                candidate.EpisodeGroupMap.TryGetValue(e, out var groupShortName);
                return new EpisodeCoverage { Type = e.Type, Number = e.Number, GroupShortName = groupShortName };
            })
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Number)
            .ToList();

        return BuildName(candidate, source, episodes, includeResolution, includeSource, includeVersion);
    }

    /// <summary>
    /// Formats a set of episode numbers as a compact range string, e.g. "1-3, 5, 7-9".
    /// </summary>
    private static string FormatEpisodeRange(IEnumerable<int> numbers)
    {
        var sorted = numbers.Distinct().OrderBy(n => n).ToList();
        if (sorted.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var start = sorted[0];
        var prev = sorted[0];
        for (var i = 1; i <= sorted.Count; i++)
        {
            var cur = i < sorted.Count ? sorted[i] : -1;
            if (cur == prev + 1)
            {
                prev = cur;
                continue;
            }
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(start == prev ? $"{start}" : $"{start}-{prev}");
            start = prev = cur;
        }
        return sb.ToString();
    }

    /// <summary>
    /// A single file location belonging to a release candidate.
    /// </summary>
    public class File
    {
        /// <summary>VideoLocal_Place ID.</summary>
        [Required]
        public required int PlaceID { get; init; }

        /// <summary>VideoLocal ID (unique file identity, based on ED2K hash + size).</summary>
        [Required]
        public required int VideoLocalID { get; init; }

        /// <summary>
        /// Absolute file path, or null if the managed folder is currently unavailable.
        /// </summary>
        public string? AbsolutePath { get; init; }

        /// <summary>File size in bytes.</summary>
        [Required]
        public required long FileSize { get; init; }

        /// <summary>
        /// True when this specific file would be deleted by auto-management.
        /// For airing series, individual files within a candidate can differ.
        /// </summary>
        [Required]
        public required bool IsRedundant { get; init; }

        /// <summary>
        /// True if this file has chapter markers; false if chapter data is present
        /// but no chapters; null if chapter data is absent.
        /// </summary>
        public bool? IsChaptered { get; init; }

        /// <summary>True if this file is marked as censored by the release provider.</summary>
        public bool? IsCensored { get; init; }

        /// <summary>True if this file is marked as creditless by the release provider.</summary>
        public bool? IsCreditless { get; init; }

        /// <summary>True if this file is marked as corrupted by the release provider.</summary>
        public bool IsCorrupted { get; init; }

        /// <summary>Release source for this specific file (e.g. "BluRay", "Web").</summary>
        public string? Source { get; init; }

        /// <summary>Video resolution for this specific file (e.g. "1080p", "720p").</summary>
        public string? Resolution { get; init; }

        /// <summary>Video codec for this specific file (e.g. "HEVC", "H264").</summary>
        public string? VideoCodec { get; init; }

        /// <summary>Video bit depth for this specific file.</summary>
        public int BitDepth { get; init; }

        /// <summary>Primary audio codec for this specific file (e.g. "AAC", "AC3").</summary>
        public string? AudioCodec { get; init; }

        /// <summary>Number of audio streams in this specific file.</summary>
        public int AudioStreamCount { get; init; }

        /// <summary>Number of subtitle streams in this specific file.</summary>
        public int SubtitleStreamCount { get; init; }

        /// <summary>Release version for this specific file.</summary>
        public int Version { get; init; }

        /// <summary>Audio languages embedded in this file, from the release provider or MediaInfo.</summary>
        public IReadOnlyList<string> AudioLanguages { get; init; } = [];

        /// <summary>Subtitle languages embedded in this file, from the release provider or MediaInfo.</summary>
        public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];

        /// <summary>
        /// Episodes this specific file covers. Empty when the file has no
        /// <c>StoredReleaseInfo</c> record (use the candidate-level
        /// <see cref="ReleaseCandidate.Episodes"/> as a fallback in that case).
        /// </summary>
        [Required]
        public required IReadOnlyList<EpisodeCoverage> Episodes { get; init; }
    }

    /// <summary>
    /// An episode covered by a release candidate, with the group that provides it.
    /// </summary>
    public class EpisodeCoverage
    {
        [Required]
        public required EpisodeType Type { get; init; }

        [Required]
        public required int Number { get; init; }

        /// <summary>
        /// Short name of the release group that provides this episode in the
        /// candidate. Null when the file has no provider-backed release info
        /// (manually linked or unrecognised files).
        /// </summary>
        public string? GroupShortName { get; init; }
    }
}
