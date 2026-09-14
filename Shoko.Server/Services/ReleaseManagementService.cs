using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Release.Management;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;
using ApiReleaseCandidate = Shoko.Server.API.v3.Models.Release.ReleaseCandidate;

namespace Shoko.Server.Services;

/// <summary>
/// Plugin-facing facade over the release management system. Wraps the internal
/// grouping, comparison, and auto-management services (and their server
/// models) into the plugin abstraction models exposed by
/// <see cref="Abstractions.Video.Services.IReleaseManagementService"/>.
/// </summary>
public class ReleaseManagementService(
    AnimeSeriesRepository animeSeries,
    VideoLocalRepository videoLocals,
    VideoLocal_PlaceRepository videoLocalPlaces,
    AniDB_Anime_TitleRepository anidbTitles,
    VideoReleaseGroupingService grouper,
    ReleaseComparisonService comparer,
    ReleaseAutoManagementService autoManagement,
    IQueueScheduler scheduler
) : IReleaseManagementService
{
    /// <inheritdoc/>
    public (IReadOnlyList<SeriesWithCandidates> Page, int TotalCount) GetSeriesWithCandidates(
        bool onlyFinishedSeries = false, bool onlyWithRedundant = false, bool includeVariations = false,
        string? search = null, int pageSize = 100, int page = 1)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : AniDB_Anime_TitleRepository.NormalizeForSearch(search);

        // Safe (never under-inclusive) pre-filter over CrossRef_File_Episode, cutting
        // the library down before any per-series grouping work runs — same as the
        // release management UI endpoint.
        var animeIDsWithMultipleFilesPerEpisode = grouper.GetAnimeIDsWithMultipleFilesPerEpisode();

        var allSeries = animeSeries.GetAll()
            .Where(s => animeIDsWithMultipleFilesPerEpisode.Contains(s.AniDB_ID))
            .Where(s => !onlyFinishedSeries || (s.AniDB_Anime?.GetFinishedAiring() ?? false))
            .Where(s => normalizedSearch == null || anidbTitles.AnimeMatchesSearch(s.AniDB_ID, normalizedSearch))
            .ToList();

        var qualifying = allSeries
            .Select(series =>
            {
                QualifyingSeries? none = null;
                var videoLookup = BuildVideoLookup(series, includeVariations);
                if (videoLookup.Count <= 1)
                    return none;
                if (!grouper.MightHaveMultipleCandidates(videoLookup.Values, series.AniDB_ID))
                    return none;

                var computation = ComputeCandidates(series, videoLookup, includeVariations,
                    bypassEligibilityGate: false, preferredCandidateKey: null);
                if (computation is not { } value)
                    return none;

                var hasRedundant = value.Ranked.Any(c => c.Places.Count > 0 && c.Places.All(p => value.RedundantPlaceIds.Contains(p.ID)));
                if (onlyWithRedundant && !hasRedundant)
                    return none;

                return new QualifyingSeries(series, videoLookup, value);
            })
            .Where(q => q is not null)
            .Select(q => q!.Value)
            .ToList();

        var pageItems = pageSize <= 0
            ? qualifying
            : qualifying.Skip(pageSize * (page - 1)).Take(pageSize).ToList();

        var results = pageItems
            .OrderBy(q => q.Series.Title)
            .Select(q => BuildSeriesWithCandidates(q.Series, q.VideoLookup, includeVariations,
                includeOverrides: false, computation: q.Computation)!)
            .ToList();

        return (results, qualifying.Count);
    }

    /// <summary>
    /// A series that passed qualification, with its video lookup and computed
    /// candidates carried along so the page-build step doesn't recompute them.
    /// </summary>
    private readonly record struct QualifyingSeries(
        AnimeSeries Series, Dictionary<int, VideoLocal> VideoLookup, CandidateComputation Computation);

    /// <inheritdoc/>
    public SeriesWithCandidates? GetSeriesCandidates(
        IShokoSeries series, bool includeVariations = false, bool includeOverrides = false,
        string? preferredCandidateKey = null)
    {
        if (series is not AnimeSeries shokoSeries)
            throw new ArgumentException("Series is not a local Shoko series.", nameof(series));

        var videoLookup = BuildVideoLookup(shokoSeries, includeVariations);

        // A caller actively inspecting a single series' candidates is treated the
        // same as the detail view: the top-ranked candidate acts as an implicit
        // selection bypassing the automatic-redundancy eligibility gate (which only
        // exists to protect unattended decisions), same as an explicit preferred key.
        var computation = ComputeCandidates(shokoSeries, videoLookup,
            includeVariations, bypassEligibilityGate: includeOverrides || preferredCandidateKey is not null,
            preferredCandidateKey);

        return BuildSeriesWithCandidates(shokoSeries, videoLookup,
            includeVariations, includeOverrides, preferredCandidateKey, computation);
    }

    /// <inheritdoc/>
    public ReleaseDeletionPreview? GetSeriesDeletionPreview(
        IShokoSeries series, bool includeVariations = false, string? preferredCandidateKey = null)
    {
        if (series is not AnimeSeries shokoSeries)
            throw new ArgumentException("Series is not a local Shoko series.", nameof(series));

        var videoLookup = BuildVideoLookup(shokoSeries, includeVariations, distinctByVideoID: true);
        var places = videoLookup.Values
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return null;

        var candidates = grouper.Group(places, shokoSeries.AniDB_ID);
        if (candidates.Count <= 1)
            return null;

        var ranked = comparer.Rank(candidates).ToList();

        // An explicit preferred candidate is a deliberate user choice, so it bypasses
        // the automatic-redundancy eligibility gate even for a mixed/gap-fill primary
        // that could never qualify on its own.
        var isOverridden = preferredCandidateKey is not null && ranked.Any(c => c.Key == preferredCandidateKey);
        if (isOverridden)
        {
            var preferredIdx = ranked.FindIndex(c => c.Key == preferredCandidateKey);
            if (preferredIdx > 0)
            {
                var preferred = ranked[preferredIdx];
                ranked.RemoveAt(preferredIdx);
                ranked.Insert(0, preferred);
            }
        }

        var redundantPlaces = autoManagement.ComputeRedundantPlaces(shokoSeries, ranked, videoLookup,
            bypassEligibilityGate: isOverridden);
        if (redundantPlaces.Count == 0)
            return null;

        return BuildDeletionPreview(shokoSeries, redundantPlaces, videoLookup);
    }

    /// <inheritdoc/>
    public ReleaseDeletionPreview GetOverrideDeletionPreview(
        IShokoSeries series, IReadOnlyCollection<int> selectedPlaceIDs, bool includeVariations = false)
    {
        if (series is not AnimeSeries shokoSeries)
            throw new ArgumentException("Series is not a local Shoko series.", nameof(series));

        var videoLookup = BuildVideoLookup(shokoSeries, includeVariations, distinctByVideoID: true);
        if (videoLookup.Count == 0)
            throw new InvalidOperationException("No files found for this series.");

        var places = videoLookup.Values
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            throw new InvalidOperationException("No file locations found for this series.");

        var selectedPlaceIds = selectedPlaceIDs.ToHashSet();

        var allPlaceIds = places.Select(p => p.ID).ToHashSet();
        var unknownIds = selectedPlaceIds.Where(id => !allPlaceIds.Contains(id)).ToList();
        if (unknownIds.Count > 0)
            throw new ArgumentException($"Place IDs not found for this series: {string.Join(", ", unknownIds)}",
                nameof(selectedPlaceIDs));

        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup, shokoSeries.AniDB_ID));

        var allCoveredEpisodes = placeEpisodeCoverage.Values.SelectMany(x => x).ToHashSet();
        var selectionCoveredEpisodes = selectedPlaceIds
            .Where(id => allPlaceIds.Contains(id) && placeEpisodeCoverage.ContainsKey(id))
            .SelectMany(id => placeEpisodeCoverage[id])
            .ToHashSet();

        if (!allCoveredEpisodes.IsSubsetOf(selectionCoveredEpisodes))
            throw new ArgumentException("The selection does not cover all episodes that have files. Some episodes would be left without a file.",
                nameof(selectedPlaceIDs));

        var placesToDelete = places.Where(p => !selectedPlaceIds.Contains(p.ID)).ToList();

        return BuildDeletionPreview(shokoSeries, placesToDelete, videoLookup);
    }

    /// <inheritdoc/>
    public async Task QueueDeletion(IReadOnlyCollection<int> placeIDs)
    {
        if (placeIDs.Count == 0)
            throw new ArgumentException("No place IDs provided.", nameof(placeIDs));

        await scheduler.StartJob<DeleteRedundantReleasesJob>(j => j.PlaceIDs = placeIDs.ToList());
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Shared inputs for building a series' candidates: grouped, ranked, and
    /// redundancy-computed data, without the display-name work.
    /// </summary>
    private readonly record struct CandidateComputation(
        IReadOnlyList<VideoReleaseCandidate> Ranked,
        HashSet<int> RedundantPlaceIds);

    private Dictionary<int, VideoLocal> BuildVideoLookup(
        AnimeSeries series, bool includeVariations, bool distinctByVideoID = false)
    {
        var videos = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
            .Where(v => includeVariations || !v.IsVariation);
        return distinctByVideoID
            ? videos.DistinctBy(v => v.VideoLocalID).ToDictionary(v => v.VideoLocalID)
            : videos.DistinctBy(v => (v.Hash, v.FileSize)).ToDictionary(v => v.VideoLocalID);
    }

    private CandidateComputation? ComputeCandidates(
        AnimeSeries series,
        Dictionary<int, VideoLocal> videoLookup,
        bool includeVariations,
        bool bypassEligibilityGate,
        string? preferredCandidateKey)
    {
        if (videoLookup.Count <= 1)
            return null;

        var places = videoLookup.Values
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return null;

        var candidates = grouper.Group(places, series.AniDB_ID);
        if (candidates.Count == 0)
            return null;

        // A single candidate is still relevant when one of its episodes is covered by
        // more than one file with nothing to distinguish them (ambiguous full collision).
        var hasAmbiguousCoverage = grouper.HasAmbiguousEpisodeCoverage(candidates, videoLookup, series.AniDB_ID);
        if (candidates.Count <= 1 && !hasAmbiguousCoverage)
            return null;

        var ranked = comparer.Rank(candidates);

        // When the caller explicitly selects a candidate, redundancy is computed
        // relative to that selection instead of the natural rank-1 candidate. Display
        // order/rank numbers are unaffected.
        var redundancyBasis = ranked;
        var effectiveBypass = bypassEligibilityGate;
        if (preferredCandidateKey is not null)
        {
            var preferredIdx = ranked.ToList().FindIndex(c => c.Key == preferredCandidateKey);
            if (preferredIdx >= 0)
            {
                var reordered = ranked.ToList();
                var preferred = reordered[preferredIdx];
                reordered.RemoveAt(preferredIdx);
                reordered.Insert(0, preferred);
                redundancyBasis = reordered;
                effectiveBypass = true;
            }
        }

        var redundantPlaceIds = autoManagement.ComputeRedundantPlaces(series, redundancyBasis, videoLookup,
                bypassEligibilityGate: effectiveBypass)
            .Select(p => p.ID)
            .ToHashSet();

        return new CandidateComputation(ranked, redundantPlaceIds);
    }

    private SeriesWithCandidates? BuildSeriesWithCandidates(
        AnimeSeries series,
        Dictionary<int, VideoLocal> videoLookup,
        bool includeVariations,
        bool includeOverrides,
        string? preferredCandidateKey = null,
        CandidateComputation? computation = null)
    {
        if (computation is not { } value)
            return null;

        var (ranked, redundantPlaceIds) = value;

        var places = ranked.SelectMany(c => c.Places).ToList();
        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup, series.AniDB_ID));

        // Compute which signals actually vary across candidates so names only include
        // the qualifiers that distinguish one candidate from another.
        var includeResolution = ranked.Select(c => c.Resolution).Where(r => r is not null).Distinct().Count() > 1;
        var includeSource = ranked.Select(c => c.Source).Where(s => s != ReleaseSource.Unknown).Distinct().Count() > 1;
        var includeVersion = ranked
            .Where(c => !string.IsNullOrEmpty(c.GroupID) && !string.IsNullOrEmpty(c.GroupSource))
            .GroupBy(c => $"{c.GroupID}|{c.GroupSource}")
            .Any(g => g.Count() > 1);

        // Compute display names and disambiguate collisions the same way the release
        // management UI does.
        var names = ranked
            .Select(c => ApiReleaseCandidate.ComputeName(c, includeResolution, includeSource, includeVersion))
            .ToList();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (usedNames.Add(name))
                continue;

            var firstPath = ranked[i].Places.FirstOrDefault()?.RelativePath ?? string.Empty;
            var suffix = Path.GetFileName(Path.GetDirectoryName(firstPath));
            if (string.IsNullOrEmpty(suffix))
                suffix = Path.GetFileNameWithoutExtension(firstPath);
            if (string.IsNullOrEmpty(suffix))
                suffix = $"#{i + 1}";
            names[i] = $"{name} ({suffix})";
            usedNames.Add(names[i]);
        }

        var candidateDTOs = new List<ReleaseCandidate>(ranked.Count);
        for (var i = 0; i < ranked.Count; i++)
        {
            var candidate = ranked[i];
            var redundantFiles = candidate.Places.Where(p => redundantPlaceIds.Contains(p.ID)).ToList();
            candidateDTOs.Add(new ReleaseCandidate
            {
                Rank = i + 1,
                Key = candidate.Key,
                Name = names[i],
                HasReleaseInfo = candidate.HasReleaseInfo,
                IsRedundant = candidate.Places.Count > 0 && redundantFiles.Count == candidate.Places.Count,
                RedundantFileCount = redundantFiles.Count,
                RedundantEpisodes = redundantFiles
                    .SelectMany(p => placeEpisodeCoverage.TryGetValue(p.ID, out var cov)
                        ? cov
                        : (IReadOnlySet<(EpisodeType, int)>)new HashSet<(EpisodeType, int)>())
                    .ToHashSet(),
                GroupID = candidate.GroupID,
                GroupSource = candidate.GroupSource,
                GroupName = candidate.GroupName,
                GroupShortName = candidate.GroupShortName,
                Source = candidate.Source,
                Resolution = candidate.Resolution,
                VideoCodec = candidate.VideoCodec,
                BitDepth = candidate.BitDepth,
                AudioCodec = candidate.AudioCodec,
                Container = candidate.Container,
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
                AudioLanguages = candidate.AudioLanguages,
                SubtitleLanguages = candidate.SubtitleLanguages,
                HasPartialCoverage = candidate.HasPartialCoverage,
                EpisodeCoverage = candidate.EpisodeCoverage,
                Files = candidate.Places,
            });
        }

        IReadOnlyList<ReleaseOverride> overrideDTOs = [];
        if (includeOverrides)
        {
            var releaseOverrides = grouper.GetOverrides(places, series.AniDB_ID);
            overrideDTOs = releaseOverrides
                .Select(o => new ReleaseOverride
                {
                    Key = o.Key,
                    GroupID = o.GroupID,
                    GroupSource = o.GroupSource,
                    GroupName = o.GroupName,
                    GroupShortName = o.GroupShortName,
                    Source = o.Source,
                    Resolution = o.Resolution,
                    VideoCodec = o.VideoCodec,
                    BitDepth = o.BitDepth,
                    AudioCodec = o.AudioCodec,
                    AudioStreamCount = o.AudioStreamCount,
                    SubtitleStreamCount = o.SubtitleStreamCount,
                    AudioLanguages = o.AudioLanguages,
                    SubtitleLanguages = o.SubtitleLanguages,
                    HasPartialCoverage = o.HasPartialCoverage,
                    Files = o.Files
                        .Select(f => new ReleaseOverrideFile
                        {
                            File = f.Place,
                            Version = f.Version,
                            IsChaptered = f.IsChaptered,
                            SubtitleStreamCount = f.SubtitleStreamCount,
                            Episodes = f.Episodes,
                        })
                        .ToList(),
                })
                .ToList();
        }

        return new SeriesWithCandidates
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            IsAiring = autoManagement.IsSeriesAiring(series),
            HasRedundantCandidates = candidateDTOs.Any(c => c.IsRedundant),
            FilesToAutoDeleteCount = redundantPlaceIds.Count,
            Candidates = candidateDTOs,
            Overrides = overrideDTOs,
        };
    }

    private ReleaseDeletionPreview BuildDeletionPreview(
        AnimeSeries series,
        IReadOnlyList<VideoLocal_Place> placesToDelete,
        Dictionary<int, VideoLocal> videoLookup)
    {
        var fileLocations = placesToDelete
            .Select(place =>
            {
                videoLookup.TryGetValue(place.VideoID, out var video);
                return new ReleaseDeletionPreviewFile
                {
                    PlaceID = place.ID,
                    VideoID = place.VideoID,
                    AbsolutePath = place.Path,
                    FileSize = video?.FileSize ?? 0,
                };
            })
            .OrderBy(f => f.AbsolutePath)
            .ToList();

        return new ReleaseDeletionPreview
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            TotalFilesToDelete = fileLocations.Count,
            TotalSizeToDelete = fileLocations.Sum(f => f.FileSize),
            Files = fileLocations,
        };
    }
}
