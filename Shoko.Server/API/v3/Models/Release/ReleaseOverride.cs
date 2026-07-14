using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Video.Enums;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// All files from a single release group (after same-group merge), without the
/// coverage filter applied. Used as the data source for the Mix &amp; Match
/// release override view.
/// </summary>
public class ReleaseOverride
{
    /// <summary>
    /// Stable identifier for this override group, unique per distinct file set within
    /// a series' override list — use this (not <see cref="GroupID"/>, which is null for
    /// unnamed/unidentified groups and can collide across multiple distinct groups) to
    /// distinguish and select between entries in <c>tracks</c>/Mix &amp; Match.
    /// </summary>
    [Required]
    public required string Key { get; init; }

    public string? GroupID { get; init; }

    public string? GroupName { get; init; }

    public string? GroupShortName { get; init; }

    /// <summary>
    /// Human-readable display name built from the group's distinguishing
    /// signals: group name and resolution.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    public string? Source { get; init; }

    public string? Resolution { get; init; }

    public string? VideoCodec { get; init; }

    public int BitDepth { get; init; }

    public string? AudioCodec { get; init; }

    public int AudioStreamCount { get; init; }

    public int SubtitleStreamCount { get; init; }

    public IReadOnlyList<string> AudioLanguages { get; init; } = [];

    public IReadOnlyList<string> SubtitleLanguages { get; init; } = [];

    /// <summary>
    /// True when this group does not have files for every episode in the series.
    /// Such groups are excluded from <see cref="SeriesWithCandidates.Candidates"/>
    /// but are available here for manual release override assignment.
    /// </summary>
    [Required]
    public required bool HasPartialCoverage { get; init; }

    /// <summary>All file locations belonging to this release group.</summary>
    [Required]
    public required IReadOnlyList<OverrideFile> Files { get; init; }

    // ── Factory ─────────────────────────────────────────────────────────────

    public static ReleaseOverride FromOverride(
        VideoReleaseOverride releaseOverride,
        Dictionary<int, VideoLocal> videoLookup,
        bool includeResolution = true,
        bool includeSource = true)
    {
        var source = releaseOverride.Source switch
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

        var files = releaseOverride.Files
            .Select(f =>
            {
                videoLookup.TryGetValue(f.Place.VideoID, out var video);
                var episodes = f.Episodes
                    .Select(e => new ReleaseCandidate.EpisodeCoverage { Type = e.Type, Number = e.Number })
                    .OrderBy(e => e.Type).ThenBy(e => e.Number)
                    .ToList();
                return new OverrideFile
                {
                    PlaceID = f.Place.ID,
                    VideoLocalID = f.Place.VideoID,
                    AbsolutePath = f.Place.Path,
                    FileSize = video?.FileSize ?? 0,
                    Version = f.Version,
                    IsChaptered = f.IsChaptered,
                    Episodes = episodes,
                };
            })
            .OrderBy(f => f.Episodes.Count > 0 ? (int)f.Episodes[0].Type : int.MaxValue)
            .ThenBy(f => f.Episodes.Count > 0 ? f.Episodes[0].Number : int.MaxValue)
            .ToList();

        var name = BuildName(releaseOverride.GroupName, releaseOverride.GroupShortName, source,
            releaseOverride.Resolution, releaseOverride.AudioStreamCount, includeResolution, includeSource);

        return new ReleaseOverride
        {
            Key = releaseOverride.Key,
            GroupID = releaseOverride.GroupID,
            GroupName = releaseOverride.GroupName,
            GroupShortName = releaseOverride.GroupShortName,
            Name = name,
            Source = source,
            Resolution = releaseOverride.Resolution,
            VideoCodec = releaseOverride.VideoCodec,
            BitDepth = releaseOverride.BitDepth,
            AudioCodec = releaseOverride.AudioCodec,
            AudioStreamCount = releaseOverride.AudioStreamCount,
            SubtitleStreamCount = releaseOverride.SubtitleStreamCount,
            AudioLanguages = releaseOverride.AudioLanguages.Select(l => l.ToString()).ToList(),
            SubtitleLanguages = releaseOverride.SubtitleLanguages.Select(l => l.ToString()).ToList(),
            HasPartialCoverage = releaseOverride.HasPartialCoverage,
            Files = files,
        };
    }

    private static string BuildName(
        string? groupName, string? groupShortName, string? source, string? resolution,
        int audioStreamCount, bool includeResolution, bool includeSource)
    {
        var parts = new List<string>();
        var groupLabel = groupName ?? groupShortName;

        if (groupLabel is not null)
        {
            parts.Add(groupLabel);
            if (includeResolution && resolution is not null) parts.Add(resolution);
            if (includeSource && source is not null) parts.Add(source);
        }
        else if (source is not null)
        {
            parts.Add(source);
            if (includeResolution && resolution is not null) parts.Add(resolution);
        }
        else if (resolution is not null)
        {
            parts.Add(resolution);
        }
        else
        {
            parts.Add("Unknown");
        }

        if (audioStreamCount > 1)
            parts.Add("Multi-Audio");

        return string.Join(" ", parts);
    }

    /// <summary>A single file location within a release override group.</summary>
    public class OverrideFile
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

        /// <summary>Release version number from the provider. 0 when unknown.</summary>
        public int Version { get; init; }

        /// <summary>Whether the file has chapter marks, or null if unknown.</summary>
        public bool? IsChaptered { get; init; }

        /// <summary>Episodes this specific file covers.</summary>
        [Required]
        public required IReadOnlyList<ReleaseCandidate.EpisodeCoverage> Episodes { get; init; }
    }
}
