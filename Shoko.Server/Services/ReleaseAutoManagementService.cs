using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Checks whether newly imported videos make any existing release candidate
/// redundant and, when <see cref="ReleaseComparisonPreferences.AllowDeletion"/>
/// is set, removes the redundant files.
/// </summary>
public class ReleaseAutoManagementService(
    ISettingsProvider settingsProvider,
    VideoReleaseGroupingService grouper,
    ReleaseComparisonService comparer,
    VideoLocalRepository videoLocals,
    VideoLocal_PlaceRepository videoLocalPlaces,
    CrossRef_File_EpisodeRepository crossRefs,
    AnimeSeriesRepository animeSeries,
    AniDB_AnimeRepository anidbAnime,
    AniDB_EpisodeRepository anidbEpisodes,
    StoredReleaseInfoRepository releaseInfoRepository,
    IVideoService videoService,
    ILogger<ReleaseAutoManagementService> logger)
{
    /// <summary>
    /// Entry point called at the end of <c>FinalizeReleaseSearchJob</c>.
    /// Groups all files for each series the video belongs to, ranks the
    /// candidates, and removes redundant files according to configured preferences.
    /// No-ops when <see cref="ReleaseComparisonPreferences.AutoDeleteOnImport"/> is false.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the incoming <paramref name="video"/> was itself deleted
    /// (i.e. it was the redundant candidate); <see langword="false"/> otherwise.
    /// </returns>
    public async Task<bool> CheckAndAutoManage(IVideo video)
    {
        if (!settingsProvider.GetSettings().ReleaseComparisonPreferences.AutoDeleteOnImport)
            return false;

        var animeIDs = crossRefs.GetByEd2k(video.ED2K)
            .Select(x => x.AnimeID)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (animeIDs.Count == 0)
            return false;

        foreach (var animeID in animeIDs)
        {
            var series = animeSeries.GetByAnimeID(animeID);
            if (series is null)
                continue;

            await CheckSeriesAsync(series);
        }

        // If the VideoLocal record is gone, auto-management deleted the incoming file.
        return videoLocals.GetByID(video.ID) is null;
    }

    /// <summary>
    /// Computes which file locations would be considered redundant for a series,
    /// given a pre-ranked list of candidates and an optional override for which
    /// candidate is the primary. Does not delete anything.
    /// </summary>
    /// <param name="series">The series to evaluate.</param>
    /// <param name="ranked">Candidates already ranked best-first.</param>
    /// <param name="videoLookup">Map of VideoLocalID to VideoLocal, used for per-file coverage.</param>
    /// <param name="preferPerFile">
    /// When true and the series is airing, per-file redundancy is used instead of
    /// whole-candidate redundancy. Pass null to use the server setting.
    /// </param>
    /// <param name="bypassEligibilityGate">
    /// When true, skip the primary-eligibility check (<see cref="ReleaseComparisonService.GetEligiblePrimaryCoverage"/>)
    /// and trust <c>ranked[0]</c>'s raw episode coverage instead. Intended for callers where
    /// the caller (not this method) already knows the primary was explicitly, deliberately
    /// selected by a user — e.g. a per-series <c>PreferredCandidateKey</c> override — as opposed
    /// to fully-automatic/unattended redundancy checks (on-import auto-delete, or the default
    /// candidate-list badges), which must always use the gate.
    /// </param>
    public IReadOnlyList<VideoLocal_Place> ComputeRedundantPlaces(
        AnimeSeries series,
        IReadOnlyList<VideoReleaseCandidate> ranked,
        Dictionary<int, VideoLocal> videoLookup,
        bool? preferPerFile = null,
        bool bypassEligibilityGate = false)
    {
        if (ranked.Count <= 1)
            return [];

        // Places belonging to the primary must never be deleted, even when the same
        // physical file appears as a filler in a secondary gap-fill candidate.
        var primaryPlaceIds = ranked[0].Places.Select(p => p.ID).ToHashSet();

        var eligibleCoverage = bypassEligibilityGate
            ? ranked[0].EpisodeCoverage
            : comparer.GetEligiblePrimaryCoverage(ranked[0]);
        if (eligibleCoverage.Count == 0)
            return [];

        var prefs = settingsProvider.GetSettings().ReleaseComparisonPreferences;
        var usePerFile = (preferPerFile ?? prefs.PerFileDeletionForAiringSeries) && IsSeriesAiring(series);

        if (usePerFile)
        {
            var seenIds = new HashSet<int>();
            var result = new List<VideoLocal_Place>();
            foreach (var candidate in ranked.Skip(1))
            {
                var redundantPlaces = comparer.GetRedundantPlaces(
                    eligibleCoverage, candidate.Places, p => GetFileEpisodeCoverage(p, videoLookup));
                foreach (var place in redundantPlaces)
                {
                    if (!primaryPlaceIds.Contains(place.ID) && seenIds.Add(place.ID))
                        result.Add(place);
                }
            }
            return result;
        }
        else
        {
            var redundant = comparer.GetRedundantCandidates(eligibleCoverage, ranked.Skip(1).ToList());
            var seenIds = new HashSet<int>();
            var result = new List<VideoLocal_Place>();
            foreach (var place in redundant.SelectMany(c => c.Places))
            {
                if (!primaryPlaceIds.Contains(place.ID) && seenIds.Add(place.ID))
                    result.Add(place);
            }
            return result;
        }
    }

    private async Task CheckSeriesAsync(AnimeSeries series)
    {
        var videos = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
            .Where(v => !v.IsVariation)
            .ToList();
        var places = videos.SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID)).ToList();
        if (places.Count == 0)
            return;

        var candidates = grouper.Group(places);
        if (candidates.Count <= 1)
            return;

        var ranked = comparer.Rank(candidates);
        var prefs = settingsProvider.GetSettings().ReleaseComparisonPreferences;

        // Places belonging to the primary must never be deleted, even when the same
        // physical file appears as a filler in a secondary gap-fill candidate.
        var primaryPlaceIds = ranked[0].Places.Select(p => p.ID).ToHashSet();

        var eligibleCoverage = comparer.GetEligiblePrimaryCoverage(ranked[0]);
        if (eligibleCoverage.Count == 0)
            return;

        if (prefs.PerFileDeletionForAiringSeries && IsSeriesAiring(series))
        {
            // Per-file redundancy for airing series: delete individual files from secondary
            // candidates when the primary already covers those specific episodes, while
            // keeping files for episodes the primary hasn't reached yet.
            var primary = ranked[0];

            var videoLookup = videos.ToDictionary(v => v.VideoLocalID);
            foreach (var candidate in ranked.Skip(1))
                await ProcessCandidatePerFileAsync(primary, eligibleCoverage, candidate, videoLookup, series, prefs, primaryPlaceIds);
        }
        else
        {
            // Whole-candidate redundancy: only delete when the entire candidate is covered.
            var redundant = comparer.GetRedundantCandidates(eligibleCoverage, ranked.Skip(1).ToList());
            if (redundant.Count == 0)
                return;

            if (!prefs.AllowDeletion)
            {
                LogRedundant(series, ranked[0], redundant);
                return;
            }

            foreach (var candidate in redundant)
                await DeleteCandidateAsync(candidate, primaryPlaceIds);
        }
    }

    private async Task ProcessCandidatePerFileAsync(
        VideoReleaseCandidate primary,
        IReadOnlySet<(EpisodeType, int)> eligibleCoverage,
        VideoReleaseCandidate secondary,
        Dictionary<int, VideoLocal> videoLookup,
        AnimeSeries series,
        ReleaseComparisonPreferences prefs,
        HashSet<int> primaryPlaceIds)
    {
        var redundantPlaces = comparer.GetRedundantPlaces(
            eligibleCoverage, secondary.Places, p => GetFileEpisodeCoverage(p, videoLookup))
            .Where(p => !primaryPlaceIds.Contains(p.ID))
            .ToList();
        var keptCount = secondary.Places.Count - redundantPlaces.Count;

        if (redundantPlaces.Count == 0)
            return;

        if (!prefs.AllowDeletion)
        {
            logger.LogInformation(
                "Series {SeriesTitle} (AniDB {AnimeID}, airing): {RedundantCount}/{TotalCount} file(s) in candidate " +
                "{Key} are individually redundant against primary {PrimaryKey}. AllowDeletion is false — no files will be removed.",
                series.Title, series.AniDB_ID, redundantPlaces.Count, secondary.Places.Count, secondary.Key, primary.Key);

            if (keptCount > 0)
                logger.LogDebug(
                    "  {KeptCount} file(s) in candidate {Key} cover episodes not yet reached by the primary and will be retained.",
                    keptCount, secondary.Key);
            return;
        }

        logger.LogInformation(
            "Auto-management (airing series): deleting {RedundantCount}/{TotalCount} redundant file(s) from candidate {Key} " +
            "for series {SeriesTitle} (AniDB {AnimeID})",
            redundantPlaces.Count, secondary.Places.Count, secondary.Key, series.Title, series.AniDB_ID);

        if (keptCount > 0)
            logger.LogDebug(
                "  Retaining {KeptCount} file(s) in candidate {Key} covering episodes not yet reached by the primary.",
                keptCount, secondary.Key);

        foreach (var place in redundantPlaces)
            await videoService.DeleteVideoFile(place, removeFile: false, removeFolders: false);
    }

    public bool IsSeriesAiring(AnimeSeries series)
    {
        var anime = anidbAnime.GetByAnimeID(series.AniDB_ID);
        if (anime is null)
            return false;
        // Airing = no end date yet, or end date is in the future.
        return anime.EndDate is null || anime.EndDate > DateTime.Now;
    }

    public IReadOnlySet<(EpisodeType, int)> GetFileEpisodeCoverage(
        VideoLocal_Place place, Dictionary<int, VideoLocal> videoLookup)
    {
        if (!videoLookup.TryGetValue(place.VideoID, out var video))
            return new HashSet<(EpisodeType, int)>();
        var sri = releaseInfoRepository.GetByEd2kAndFileSize(video.Hash, video.FileSize);
        if (sri is null)
            return crossRefs.GetByEd2k(video.Hash)
                .Select(x => (
                    anidbEpisodes.GetByEpisodeID(x.EpisodeID) is { } episode
                        ? episode.EpisodeType
                        : EpisodeType.Episode,
                    x.EpisodeID))
                .Where(k => k.Item2 > 0)
                .ToHashSet();
        return sri.CrossReferences
            .Select(x => (
                anidbEpisodes.GetByEpisodeID(x.AnidbEpisodeID) is { } episode
                    ? episode.EpisodeType
                    : EpisodeType.Episode,
                x.AnidbEpisodeID))
            .Where(k => k.Item2 > 0)
            .ToHashSet();
    }

    private void LogRedundant(AnimeSeries series, VideoReleaseCandidate primary,
        IReadOnlyList<VideoReleaseCandidate> redundant)
    {
        logger.LogInformation(
            "Series {SeriesTitle} (AniDB {AnimeID}): {RedundantCount} redundant release candidate(s) identified. " +
            "Primary: {PrimaryKey}. AllowDeletion is false — no files will be removed.",
            series.Title, series.AniDB_ID, redundant.Count, primary.Key);

        foreach (var c in redundant)
            logger.LogDebug(
                "  Redundant candidate {Key} covers {EpCount} episode(s): {Episodes}",
                c.Key,
                c.EpisodeCoverage.Count,
                string.Join(", ", c.EpisodeCoverage.Select(e => $"{e.Type}:{e.EpisodeID}")));
    }

    private async Task DeleteCandidateAsync(VideoReleaseCandidate candidate, HashSet<int> primaryPlaceIds)
    {
        var placesToDelete = candidate.Places.Where(p => !primaryPlaceIds.Contains(p.ID)).ToList();
        logger.LogInformation(
            "Auto-management: deleting redundant release candidate {Key} ({FileCount} file(s))",
            candidate.Key, placesToDelete.Count);

        foreach (var place in placesToDelete)
            await videoService.DeleteVideoFile(place, removeFile: false, removeFolders: false);
    }
}
