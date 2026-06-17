using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Extensions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.API.v3.Models.Release.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
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
    AniDB_EpisodeRepository anidbEpisodes,
    AniDB_Anime_TitleRepository anidbTitles,
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
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping. Defaults to false.</param>
    /// <param name="search">Filter by series title. Matched case-insensitively against all main and official titles across all languages.</param>
    /// <param name="pageSize">Results per page (0 = unlimited).</param>
    /// <param name="page">Page number (1-based).</param>
    [HttpGet("Series")]
    public ActionResult<ListResult<SeriesWithCandidates>> GetSeriesWithMultipleReleases(
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery] bool onlyWithRedundant = false,
        [FromQuery] bool includeVariations = false,
        [FromQuery] string? search = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : AniDB_Anime_TitleRepository.NormalizeForSearch(search);

        var allSeries = animeSeries.GetAll()
            .Where(s => User.AllowedSeries(s))
            .Where(s => !onlyFinishedSeries || (s.AniDB_Anime?.GetFinishedAiring() ?? false))
            .Where(s => normalizedSearch == null || anidbTitles.AnimeMatchesSearch(s.AniDB_ID, normalizedSearch))
            .OrderBy(s => s.Title);

        // Pre-filter using only cached SRI group keys before running the full
        // grouper. MightHaveMultipleCandidates is a cheap check that skips the
        // expensive grouper for series that clearly have only one release.
        // The pre-fetched videoLookup is passed into BuildSeriesWithCandidates so
        // GetByAniDBAnimeID is not called a second time for the same series.
        var withCandidates = allSeries
            .Select(series =>
            {
                var videoLookup = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
                    .Where(v => includeVariations || !v.IsVariation)
                    .DistinctBy(v => (v.Hash, v.FileSize))
                    .ToDictionary(v => v.VideoLocalID);
                if (videoLookup.Count <= 1) return null;
                if (!grouper.MightHaveMultipleCandidates(videoLookup.Values)) return null;
                return BuildSeriesWithCandidates(series, videoLookup);
            })
            .Where(result => result is not null && result.Candidates.Count > 1)
            .Select(result => result!)
            .Where(result => !onlyWithRedundant || result.HasRedundantCandidates)
            .ToList();

        return withCandidates.ToListResult(r => r, page, pageSize);
    }

    /// <summary>
    /// Get the ranked release candidates for a specific series. Returns 404
    /// when the series has fewer than two distinct candidates.
    /// Also includes a <c>tracks</c> field with all release groups (including
    /// partial-coverage groups excluded from candidates) for Mix &amp; Match.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping. Defaults to false.</param>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<SeriesWithCandidates> GetSeriesCandidates(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool includeVariations = false)
    {
        var series = animeSeries.GetByID(seriesID);
        if (series is null)
            return NotFound();

        if (!User.AllowedSeries(series))
            return Forbid();

        var result = BuildSeriesWithCandidates(series, includeVariations: includeVariations, includeTracks: true);
        if (result is null)
            return NotFound();

        return result;
    }

    /// <summary>
    /// Preview which files would be deleted when the user manually assigns one
    /// file per episode (Mix &amp; Match release override). The selection must cover
    /// every episode that has at least one file; unselected files are returned as
    /// the deletion preview.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="body">The set of PlaceIDs to keep.</param>
    /// <param name="includeVariations">When true, include files marked as variations. Defaults to false.</param>
    [HttpPost("Series/{seriesID}/Override")]
    [Authorize("admin")]
    public ActionResult<ReleaseDeletionPreview> GetReleaseOverridePreview(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromBody] ReleaseOverrideBody body,
        [FromQuery] bool includeVariations = false)
    {
        var series = animeSeries.GetByID(seriesID);
        if (series is null)
            return NotFound();

        if (!User.AllowedSeries(series))
            return Forbid();

        var videoLookup = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
            .Where(v => includeVariations || !v.IsVariation)
            .DistinctBy(v => v.VideoLocalID)
            .ToDictionary(v => v.VideoLocalID);
        if (videoLookup.Count == 0)
            return NotFound("No files found for this series.");

        var places = videoLookup.Values
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return NotFound("No file locations found for this series.");

        var selectedPlaceIds = body.SelectedPlaceIDs.ToHashSet();

        var allPlaceIds = places.Select(p => p.ID).ToHashSet();
        var unknownIds = selectedPlaceIds.Where(id => !allPlaceIds.Contains(id)).ToList();
        if (unknownIds.Count > 0)
            return BadRequest($"Place IDs not found for this series: {string.Join(", ", unknownIds)}");

        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup));

        var allCoveredEpisodes = placeEpisodeCoverage.Values.SelectMany(x => x).ToHashSet();
        var selectionCoveredEpisodes = selectedPlaceIds
            .Where(id => allPlaceIds.Contains(id) && placeEpisodeCoverage.ContainsKey(id))
            .SelectMany(id => placeEpisodeCoverage[id])
            .ToHashSet();

        if (!allCoveredEpisodes.IsSubsetOf(selectionCoveredEpisodes))
            return BadRequest("The selection does not cover all episodes that have files. Some episodes would be left without a file.");

        var placesToDelete = places.Where(p => !selectedPlaceIds.Contains(p.ID)).ToList();

        var fileLocations = placesToDelete
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
    /// <param name="body">Preview options and series filters.</param>
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping. Defaults to false.</param>
    [HttpPost("Preview")]
    public ActionResult<IReadOnlyList<ReleaseDeletionPreview>> GetDeletionPreview(
        [FromBody] ReleaseDeletionPreviewBody? body,
        [FromQuery] bool includeVariations = false)
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
            .Select(series => ComputeSeriesPreview(series, overrides, includeVariations))
            .Where(preview => preview is not null && preview.TotalFilesToDelete > 0)
            .Select(preview => preview!)
            .OrderBy(preview => preview.SeriesTitle)
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
        Shoko.Server.Models.Shoko.AnimeSeries series,
        Dictionary<int, Shoko.Server.Models.Shoko.VideoLocal>? prefetchedVideoLookup = null,
        bool includeVariations = false,
        bool includeTracks = false)
    {
        var videoLookup = prefetchedVideoLookup ?? videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
            .Where(v => includeVariations || !v.IsVariation)
            .DistinctBy(v => (v.Hash, v.FileSize))
            .ToDictionary(v => v.VideoLocalID);

        if (videoLookup.Count <= 1)
            return null;

        var places = videoLookup.Values
            .SelectMany(v => videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();
        if (places.Count == 0)
            return null;

        var candidates = grouper.Group(places);
        if (candidates.Count <= 1)
            return null;

        var ranked = comparer.Rank(candidates);

        var redundantPlaces = autoManagement.ComputeRedundantPlaces(series, ranked, videoLookup)
            .Select(p => p.ID)
            .ToHashSet();

        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup));

        // When including tracks we need episode IDs for all places (including partial-coverage
        // groups not represented in any full candidate). Otherwise the candidates alone suffice.
        IEnumerable<int> episodeIdSource = includeTracks
            ? placeEpisodeCoverage.Values.SelectMany(cov => cov.Select(e => e.Item2))
            : ranked.SelectMany(c => c.EpisodeCoverage).Select(e => e.Number);

        var episodeLookup = episodeIdSource
            .Distinct()
            .Select(id => anidbEpisodes.GetByEpisodeID(id))
            .WhereNotNull()
            .ToDictionary(e => e.EpisodeID, e => (e.EpisodeType, e.EpisodeNumber));

        // Compute which signals actually vary across candidates so names only include
        // the qualifiers that distinguish one candidate from another.
        var candidateIncludeResolution = ranked.Select(c => c.Resolution).Where(r => r is not null).Distinct().Count() > 1;
        var candidateIncludeSource = ranked.Select(c => c.Source).Where(s => s != Shoko.Abstractions.Video.Enums.ReleaseSource.Unknown).Distinct().Count() > 1;
        var candidateIncludeVersion = ranked
            .Where(c => !string.IsNullOrEmpty(c.GroupID) && !string.IsNullOrEmpty(c.GroupSource))
            .GroupBy(c => $"{c.GroupID}|{c.GroupSource}")
            .Any(g => g.Count() > 1);

        var candidateDTOs = new List<ReleaseCandidate>(ranked.Count);
        for (var i = 0; i < ranked.Count; i++)
        {
            var candidate = ranked[i];
            var isRedundant = candidate.Places.All(p => redundantPlaces.Contains(p.ID));

            ReleaseComparisonService.CompareDecision? decision = null;
            if (i > 0)
                decision = comparer.CompareWithDecision(ranked[0], candidate);

            candidateDTOs.Add(ReleaseCandidate.FromCandidate(
                candidate, rank: i + 1, isRedundant, videoLookup, redundantPlaces, decision,
                placeEpisodeCoverage, episodeLookup,
                candidateIncludeResolution, candidateIncludeSource, candidateIncludeVersion));
        }

        IReadOnlyList<ReleaseOverride> overrideDTOs = [];
        if (includeTracks)
        {
            var releaseOverrides = grouper.GetOverrides(places);
            var overrideIncludeResolution = releaseOverrides.Select(o => o.Resolution).Where(r => r is not null).Distinct().Count() > 1;
            var overrideIncludeSource = releaseOverrides.Select(o => o.Source).Where(s => s != Shoko.Abstractions.Video.Enums.ReleaseSource.Unknown).Distinct().Count() > 1;
            overrideDTOs = releaseOverrides
                .Select(o => ReleaseOverride.FromOverride(o, videoLookup, episodeLookup, overrideIncludeResolution, overrideIncludeSource))
                .ToList();
        }

        return new SeriesWithCandidates
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            IsAiring = autoManagement.IsSeriesAiring(series),
            HasRedundantCandidates = candidateDTOs.Any(c => c.IsRedundant),
            Candidates = candidateDTOs,
            Overrides = overrideDTOs,
        };
    }

    private ReleaseDeletionPreview? ComputeSeriesPreview(
        Shoko.Server.Models.Shoko.AnimeSeries series,
        Dictionary<int, string> overrides,
        bool includeVariations = false)
    {
        var videoLookup = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
            .Where(v => includeVariations || !v.IsVariation)
            .DistinctBy(v => v.VideoLocalID)
            .ToDictionary(v => v.VideoLocalID);
        if (videoLookup.Count == 0)
            return null;

        var places = videoLookup.Values
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
