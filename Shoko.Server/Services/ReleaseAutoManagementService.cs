using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

#nullable enable
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
    IVideoService videoService,
    ILogger<ReleaseAutoManagementService> logger)
{
    /// <summary>
    /// Entry point called at the end of <c>FinalizeReleaseSearchJob</c>.
    /// Groups all files for each series the video belongs to, ranks the
    /// candidates, and deletes any that are fully covered by a better release.
    /// </summary>
    public async Task CheckAndAutoManage(VideoLocal video)
    {
        var animeIDs = crossRefs.GetByEd2k(video.Hash)
            .Select(x => x.AnimeID)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (animeIDs.Count == 0)
            return;

        foreach (var animeID in animeIDs)
        {
            var series = animeSeries.GetByAnimeID(animeID);
            if (series is null)
                continue;

            await CheckSeriesAsync(series);
        }
    }

    private async Task CheckSeriesAsync(AnimeSeries series)
    {
        // Collect every place for every video linked to this series.
        var videos = videoLocals.GetByAniDBAnimeID(series.AniDB_ID);
        var places = videos.SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID)).ToList();
        if (places.Count == 0)
            return;

        var candidates = grouper.Group(places);
        if (candidates.Count <= 1)
            return;

        var ranked = comparer.Rank(candidates);
        var redundant = comparer.GetRedundantCandidates(ranked);
        if (redundant.Count == 0)
            return;

        var prefs = settingsProvider.GetSettings().ReleaseComparisonPreferences;
        if (!prefs.AllowDeletion)
        {
            LogRedundant(series, ranked[0], redundant);
            return;
        }

        foreach (var candidate in redundant)
            await DeleteCandidateAsync(candidate);
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
                string.Join(", ", c.EpisodeCoverage.Select(e => $"{e.Type}:{e.Number}")));
    }

    private async Task DeleteCandidateAsync(VideoReleaseCandidate candidate)
    {
        logger.LogInformation(
            "Auto-management: deleting redundant release candidate {Key} ({FileCount} file(s))",
            candidate.Key, candidate.Places.Count);

        foreach (var place in candidate.Places)
            await videoService.DeleteVideoFile(place, removeFile: false, removeFolders: false);
    }
}
