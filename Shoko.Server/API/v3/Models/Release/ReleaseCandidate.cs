using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#nullable enable
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
    /// True when the primary candidate (rank 1) fully covers every episode this
    /// candidate covers, making it safe to delete.
    /// </summary>
    [Required]
    public required bool IsRedundant { get; init; }

    /// <summary>
    /// The quality signal that caused this candidate to rank below the previous
    /// one in the ordered list. Null for the primary (rank 1) candidate.
    /// </summary>
    public ReleaseSignalType? DecidingSignal { get; init; }

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
        ReleaseComparisonService.CompareDecision? decision = null)
    {
        var files = candidate.Places
            .Select(place =>
            {
                videoLookup.TryGetValue(place.VideoID, out var video);
                return new File
                {
                    PlaceID = place.ID,
                    VideoLocalID = place.VideoID,
                    AbsolutePath = place.Path,
                    FileSize = video?.FileSize ?? 0,
                    IsRedundant = redundantPlaceIDs?.Contains(place.ID) ?? isRedundant,
                };
            })
            .ToList();

        var episodes = candidate.EpisodeCoverage
            .Select(e => new EpisodeCoverage { Type = e.Type, Number = e.Number })
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

        return new ReleaseCandidate
        {
            Rank = rank,
            Key = candidate.Key,
            IsRedundant = isRedundant,
            DecidingSignal = decision?.DecidingSignal,
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
            AudioLanguages = candidate.AudioLanguages.Select(l => l.ToString()).ToList(),
            SubtitleLanguages = candidate.SubtitleLanguages.Select(l => l.ToString()).ToList(),
            Files = files,
            Episodes = episodes,
        };
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
    }

    /// <summary>
    /// An episode covered by a release candidate.
    /// </summary>
    public class EpisodeCoverage
    {
        [Required]
        public required EpisodeType Type { get; init; }

        [Required]
        public required int Number { get; init; }
    }
}
