using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.API.v3.Models.Release.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/ReleaseManagement/MultipleReleases")]
[ApiV3]
[Authorize]
public class ReleaseManagementMultipleReleasesController(
    ISettingsProvider settingsProvider,
    AnimeSeriesRepository animeSeries,
    VideoLocalRepository videoLocals,
    VideoLocal_PlaceRepository videoLocalPlaces,
    VideoReleaseGroupingService grouper,
    ReleaseComparisonService comparer,
    ReleaseAutoManagementService autoManagement,
    IQueueScheduler scheduler
) : BaseController(settingsProvider)
{
    // ── Series listing ───────────────────────────────────────────────────────

    /// <summary>
    /// Get a paginated list of series that have more than one release candidate.
    /// Only series where at least two distinct candidates were identified are
    /// returned. Results are sorted by series title for stable pagination.
    /// </summary>
    /// <param name="onlyFinishedSeries">When true, only include series that have finished airing.</param>
    /// <param name="onlyWithRedundant">When true, only include series that have at least one fully redundant candidate.</param>
    /// <param name="pageSize">Results per page (0 = unlimited).</param>
    /// <param name="page">Page number (1-based).</param>
    [HttpGet("Series")]
    public ActionResult<ListResult<SeriesWithCandidates>> GetSeriesWithMultipleReleases(
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery] bool onlyWithRedundant = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var allSeries = animeSeries.GetAll()
            .Where(s => User.AllowedSeries(s))
            .Where(s => !onlyFinishedSeries || (s.AniDB_Anime?.GetFinishedAiring() ?? false))
            .OrderBy(s => s.Title);

        var withCandidates = allSeries
            .Select(series => BuildSeriesWithCandidates(series))
            .Where(result => result is not null && result.Candidates.Count > 1)
            .Select(result => result!)
            .Where(result => !onlyWithRedundant || result.HasRedundantCandidates);

        return withCandidates.ToListResult(r => r, page, pageSize);
    }

    /// <summary>
    /// Get the ranked release candidates for a specific series. Returns 404
    /// when the series has fewer than two distinct candidates.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<SeriesWithCandidates> GetSeriesCandidates(
        [FromRoute, Range(1, int.MaxValue)] int seriesID)
    {
        var series = animeSeries.GetByID(seriesID);
        if (series is null)
            return NotFound();

        if (!User.AllowedSeries(series))
            return Forbid();

        var result = BuildSeriesWithCandidates(series);
        if (result is null)
            return NotFound();

        return result;
    }

    // ── Preview & execute ────────────────────────────────────────────────────

    /// <summary>
    /// Compute a preview of which files would be deleted across the selected
    /// series. Optionally restrict to specific series via
    /// <c>includedSeriesIDs</c>, exclude specific series via
    /// <c>excludedSeriesIDs</c>, or override which candidate is treated as the
    /// primary for a given series via <c>overrides</c>.
    /// </summary>
    /// <remarks>
    /// The preview does not delete anything. Use <c>POST /Execute</c> with the
    /// returned <c>PlaceID</c> values to queue the actual deletion.
    /// When <c>includedSeriesIDs</c> is provided it takes priority over
    /// <c>excludedSeriesIDs</c>.
    /// </remarks>
    [HttpPost("Preview")]
    public ActionResult<IReadOnlyList<ReleaseDeletionPreview>> GetDeletionPreview(
        [FromBody] ReleaseDeletionPreviewBody? body)
    {
        var overrides = body?.Overrides?.ToDictionary(o => o.SeriesID, o => o.PreferredCandidateKey)
                        ?? new Dictionary<int, string>();

        IEnumerable<Shoko.Server.Models.Shoko.AnimeSeries> seriesSource;
        if (body?.IncludedSeriesIDs is { Count: > 0 } included)
        {
            var includedSet = included.ToHashSet();
            seriesSource = animeSeries.GetAll()
                .Where(s => User.AllowedSeries(s) && includedSet.Contains(s.AnimeSeriesID));
        }
        else
        {
            var excludedSet = body?.ExcludedSeriesIDs?.ToHashSet() ?? [];
            seriesSource = animeSeries.GetAll()
                .Where(s => User.AllowedSeries(s) && !excludedSet.Contains(s.AnimeSeriesID));
        }

        var result = seriesSource
            .Select(series => ComputeSeriesPreview(series, overrides))
            .Where(preview => preview is not null && preview.TotalFilesToDelete > 0)
            .Select(preview => preview!)
            .ToList();

        return result;
    }

    /// <summary>
    /// Queue a job to delete the specified file locations. Typically the
    /// <c>PlaceID</c> values come from <c>POST /Preview</c>, but the caller
    /// may supply any subset.
    /// </summary>
    /// <remarks>
    /// Deletion is queued as a background job. File-deleted events are emitted
    /// via SignalR (<c>/signalr/aggregate</c>) as each file is removed.
    /// </remarks>
    [HttpPost("Execute")]
    [Authorize("admin")]
    public async Task<ActionResult> ExecuteDeletion([FromBody] DeleteReleasesBody body)
    {
        if (body.PlaceIDs.Count == 0)
            return BadRequest("No place IDs provided.");

        await scheduler.StartJob<DeleteRedundantReleasesJob>(j => j.PlaceIDs = body.PlaceIDs);
        return Ok();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private SeriesWithCandidates? BuildSeriesWithCandidates(
        Shoko.Server.Models.Shoko.AnimeSeries series)
    {
        var videos = videoLocals.GetByAniDBAnimeID(series.AniDB_ID);
        if (videos.Count == 0)
            return null;

        var places = videos
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return null;

        var candidates = grouper.Group(places);
        if (candidates.Count <= 1)
            return null;

        var ranked = comparer.Rank(candidates);
        var videoLookup = videos.ToDictionary(v => v.VideoLocalID);

        var redundantPlaces = autoManagement.ComputeRedundantPlaces(series, ranked, videoLookup)
            .Select(p => p.ID)
            .ToHashSet();

        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup));

        var candidateDTOs = new List<ReleaseCandidate>(ranked.Count);
        for (var i = 0; i < ranked.Count; i++)
        {
            var candidate = ranked[i];
            var isRedundant = candidate.Places.All(p => redundantPlaces.Contains(p.ID));

            ReleaseComparisonService.CompareDecision? decision = null;
            if (i > 0)
                decision = comparer.CompareWithDecision(ranked[0], candidate);

            candidateDTOs.Add(ReleaseCandidate.FromCandidate(
                candidate, rank: i + 1, isRedundant, videoLookup, redundantPlaces, decision, placeEpisodeCoverage));
        }

        return new SeriesWithCandidates
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            IsAiring = autoManagement.IsSeriesAiring(series),
            HasRedundantCandidates = candidateDTOs.Any(c => c.IsRedundant),
            Candidates = candidateDTOs,
        };
    }

    private ReleaseDeletionPreview? ComputeSeriesPreview(
        Shoko.Server.Models.Shoko.AnimeSeries series,
        Dictionary<int, string> overrides)
    {
        var videos = videoLocals.GetByAniDBAnimeID(series.AniDB_ID);
        if (videos.Count == 0)
            return null;

        var places = videos
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return null;

        var candidates = grouper.Group(places);
        if (candidates.Count <= 1)
            return null;

        var ranked = comparer.Rank(candidates).ToList();

        if (overrides.TryGetValue(series.AnimeSeriesID, out var preferredKey))
        {
            var preferredIdx = ranked.FindIndex(c => c.Key == preferredKey);
            if (preferredIdx > 0)
            {
                var preferred = ranked[preferredIdx];
                ranked.RemoveAt(preferredIdx);
                ranked.Insert(0, preferred);
            }
        }

        var videoLookup = videos.ToDictionary(v => v.VideoLocalID);
        var redundantPlaces = autoManagement.ComputeRedundantPlaces(series, ranked, videoLookup);
        if (redundantPlaces.Count == 0)
            return null;

        var fileLocations = redundantPlaces
            .Select(place =>
            {
                videoLookup.TryGetValue(place.VideoID, out var video);
                return new ReleaseDeletionPreview.FileLocation
                {
                    PlaceID = place.ID,
                    VideoLocalID = place.VideoID,
                    AbsolutePath = place.Path,
                    FileSize = video?.FileSize ?? 0,
                };
            })
            .ToList();

        return new ReleaseDeletionPreview
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            TotalFilesToDelete = fileLocations.Count,
            TotalSizeToDelete = fileLocations.Sum(p => p.FileSize),
            Files = fileLocations,
        };
    }
}
