using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Enums;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.API.v3.Models.Release.Input;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CA1822
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
        var __log = LogManager.GetCurrentClassLogger();
        var __sw = Stopwatch.StartNew();

        // A series can only have more than one release candidate if some episode has more
        // than one distinct, currently-existing video file — computed once via a single pass
        // over CrossRef_File_Episode rather than per-series repository calls. This is a safe
        // (never under-inclusive) pre-filter that typically cuts the anime library down by
        // more than an order of magnitude before any per-series work runs at all.
        var animeIDsWithMultipleFilesPerEpisode = grouper.GetAnimeIDsWithMultipleFilesPerEpisode();
        __log.Info($"[PERF] GetAnimeIDsWithMultipleFilesPerEpisode: {__sw.ElapsedMilliseconds}ms, {animeIDsWithMultipleFilesPerEpisode.Count} anime IDs");
        __sw.Restart();

        var allSeries = animeSeries.GetAll()
            .Where(s => animeIDsWithMultipleFilesPerEpisode.Contains(s.AniDB_ID))
            .Where(s => User.AllowedSeries(s))
            .Where(s => !onlyFinishedSeries || (s.AniDB_Anime?.GetFinishedAiring() ?? false))
            .Where(s => normalizedSearch == null || anidbTitles.AnimeMatchesSearch(s.AniDB_ID, normalizedSearch))
            .ToList();
        __log.Info($"[PERF] allSeries filter: {__sw.ElapsedMilliseconds}ms, {allSeries.Count} series");
        __sw.Restart();

        // Cheap-ish qualification pass over every matching series. This still runs
        // grouping/ranking/redundancy per series via ComputeCandidates (needed to know
        // candidate counts, HasRedundantCandidates, and sort order), but does NOT build
        // the far more expensive ReleaseCandidate DTOs (name computation with collision
        // detection across all candidates, per-file quality-signal formatting, episode-
        // coverage resolution) — those only get built for the page actually returned,
        // below. Deliberately sequential: with the animeIDsWithMultipleFilesPerEpisode
        // pre-filter already cutting this down to a small set, PLINQ's per-request thread-
        // pool scheduling overhead costs far more than it saves here.
        // MightHaveMultipleCandidates is a cheap pre-check that skips ComputeCandidates
        // entirely for series that clearly have only one release. The pre-fetched
        // videoLookup is passed into ComputeCandidates/BuildSeriesWithCandidates so
        // GetByAniDBAnimeID is not called a second time for the same series.
        var qualifying = allSeries
            .Select(series =>
            {
                QualifyingSeries? none = null;
                var videoLookup = videoLocals.GetByAniDBAnimeID(series.AniDB_ID)
                    .Where(v => includeVariations || !v.IsVariation)
                    .DistinctBy(v => (v.Hash, v.FileSize))
                    .ToDictionary(v => v.VideoLocalID);
                if (videoLookup.Count <= 1)
                    return none;
                if (!grouper.MightHaveMultipleCandidates(videoLookup.Values))
                    return none;

                var computation = ComputeCandidates(series, videoLookup, includeVariations,
                    bypassEligibilityGate: false, preferredCandidateKey: null);
                if (computation is not { Ranked.Count: > 1 } value)
                    return none;

                var hasRedundant = value.Ranked.Any(c => c.Places.Count > 0 && c.Places.All(p => value.RedundantPlaceIds.Contains(p.ID)));
                if (onlyWithRedundant && !hasRedundant)
                    return none;

                return new QualifyingSeries(series, videoLookup, value);
            })
            .Where(q => q is not null)
            .Select(q => q!.Value)
            .OrderBy(q => q.Series.Title)
            .ToList();
        __log.Info($"[PERF] qualify pass: {__sw.ElapsedMilliseconds}ms, {qualifying.Count} qualifying series (of {allSeries.Count} candidates)");
        __sw.Restart();

        // Build the full ReleaseCandidate DTOs only for the page being returned. Deliberately
        // sequential (not ToListResult's PLINQ-based overload) — for a handful of items this
        // avoids PLINQ's per-request thread-pool scheduling overhead, which otherwise dwarfs
        // the actual per-series DTO-building cost.
        var pageItems = pageSize <= 0 ? qualifying : qualifying.Skip(pageSize * (page - 1)).Take(pageSize).ToList();
        var results = pageItems
            .Select(q => BuildSeriesWithCandidates(q.Series, q.VideoLookup, includeVariations, includeTracks: false, prefetchedComputation: q.Computation)!)
            .ToList();
        __log.Info($"[PERF] page build: {__sw.ElapsedMilliseconds}ms, {results.Count} series built");

        return new ListResult<SeriesWithCandidates>(qualifying.Count, results);
    }

    /// <summary>
    /// A series that passed the cheap qualification pass, with its video lookup and
    /// already-computed candidates/redundancy carried along so the page-build step doesn't
    /// have to re-run grouping/ranking/redundancy from scratch.
    /// </summary>
    private readonly record struct QualifyingSeries(AnimeSeries Series, Dictionary<int, VideoLocal> VideoLookup, CandidateComputation Computation);

    /// <summary>
    /// Get the ranked release candidates for a specific series. Returns 404
    /// when the series has fewer than two distinct candidates.
    /// Also includes a <c>tracks</c> field with all release groups (including
    /// partial-coverage groups excluded from candidates) for Mix &amp; Match.
    /// </summary>
    /// <param name="seriesID">Shoko series ID.</param>
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping. Defaults to false.</param>
    /// <param name="preferredCandidateKey">
    /// When set to one of the returned candidates' <c>Key</c>, <c>IsRedundant</c>/<c>RedundantFileCount</c>/
    /// <c>RedundantEpisodes</c> (and <c>HasRedundantCandidates</c>/<c>FilesToAutoDeleteCount</c>) are recomputed
    /// treating that candidate as the kept selection instead of the natural rank-1 candidate — this is an explicit,
    /// user-driven choice, so it bypasses the automatic-redundancy eligibility gate that protects unattended
    /// deletion. Display order/<c>Rank</c> numbers are unaffected. Ignored if the key doesn't match any candidate.
    /// </param>
    [HttpGet("Series/{seriesID}")]
    public ActionResult<SeriesWithCandidates> GetSeriesCandidates(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool includeVariations = false,
        [FromQuery] string? preferredCandidateKey = null)
    {
        var series = animeSeries.GetByID(seriesID);
        if (series is null)
            return NotFound();

        if (!User.AllowedSeries(series))
            return Forbid();

        var result = BuildSeriesWithCandidates(series, includeVariations: includeVariations, includeTracks: true, preferredCandidateKey: preferredCandidateKey);
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
            .OrderBy(f => f.AbsolutePath)
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
    /// <param name="onlyFinishedSeries">When true, don't process currently airing series.</param>
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping. Defaults to false.</param>
    [HttpPost("Preview")]
    public ActionResult<IReadOnlyList<ReleaseDeletionPreview>> GetDeletionPreview(
        [FromBody] ReleaseDeletionPreviewBody? body,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery] bool includeVariations = false)
    {
        var overrides = body?.Overrides?.ToDictionary(o => o.SeriesID, o => o.PreferredCandidateKey)
                        ?? new Dictionary<int, string>();

        IEnumerable<AnimeSeries> seriesSource;
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
                .Where(s => User.AllowedSeries(s) && (!onlyFinishedSeries || (s.AniDB_Anime?.GetFinishedAiring() ?? false)) && !excludedSet.Contains(s.AnimeSeriesID));
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

    /// <summary>
    /// Shared inputs for building a series' candidate response: computed once and reused
    /// by both the cheap list-qualification pass and the full per-series DTO builder.
    /// </summary>
    private readonly record struct CandidateComputation(
        Dictionary<int, VideoLocal> VideoLookup,
        List<VideoLocal_Place> Places,
        IReadOnlyList<VideoReleaseCandidate> Ranked,
        HashSet<int> RedundantPlaceIds);

    /// <summary>
    /// Builds a series' video lookup, grouped/ranked candidates, and redundant-place set —
    /// the expensive-but-unavoidable part of the pipeline (grouping, ranking, redundancy),
    /// without building the much more expensive <see cref="ReleaseCandidate"/> DTOs (name
    /// computation with collision detection, per-file quality-signal formatting, episode
    /// resolution). Returns null when the series doesn't have enough files/candidates to
    /// be relevant.
    /// </summary>
    /// <param name="series">The series to compute candidates for.</param>
    /// <param name="prefetchedVideoLookup">Already-built video lookup, if available, to avoid re-querying.</param>
    /// <param name="includeVariations">When true, include files marked as variations in the candidate grouping.</param>
    /// <param name="bypassEligibilityGate">
    /// See <see cref="ReleaseAutoManagementService.ComputeRedundantPlaces"/> — whether the
    /// natural rank-1 candidate (or the <paramref name="preferredCandidateKey"/> candidate,
    /// if given) is treated as an explicit selection that bypasses the automatic-redundancy
    /// eligibility gate.
    /// </param>
    /// <param name="preferredCandidateKey">When set, treat this candidate's key as the selected primary instead of the natural rank-1 candidate.</param>
    private CandidateComputation? ComputeCandidates(
        AnimeSeries series,
        Dictionary<int, VideoLocal>? prefetchedVideoLookup,
        bool includeVariations,
        bool bypassEligibilityGate,
        string? preferredCandidateKey)
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

        // Display order/Rank numbers always reflect natural quality ranking.
        var ranked = comparer.Rank(candidates);

        // When the caller explicitly selects a candidate (e.g. "Select as primary" in the
        // UI), redundancy is computed relative to that selection instead of the natural
        // rank-1 candidate. This only affects which places are considered redundant, not
        // the displayed Rank/ordering.
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

        var redundantPlaceIds = autoManagement.ComputeRedundantPlaces(series, redundancyBasis, videoLookup, bypassEligibilityGate: effectiveBypass)
            .Select(p => p.ID)
            .ToHashSet();

        return new CandidateComputation(videoLookup, places, ranked, redundantPlaceIds);
    }

    private SeriesWithCandidates? BuildSeriesWithCandidates(
        AnimeSeries series,
        Dictionary<int, VideoLocal>? prefetchedVideoLookup = null,
        bool includeVariations = false,
        bool includeTracks = false,
        string? preferredCandidateKey = null,
        CandidateComputation? prefetchedComputation = null)
    {
        // On the single-series detail view (includeTracks: true), a human is actively
        // reviewing this series' candidates, so the top-ranked candidate is treated as an
        // implicit selection by default — bypassing the automatic-eligibility gate, same as
        // an explicit "Select as primary". The paginated list view (includeTracks: false)
        // has no such implicit selection and stays gated, matching automatic/batch behavior.
        // When the caller already ran ComputeCandidates with the exact same parameters (e.g.
        // the series-list qualification pass), reuse it instead of re-running grouping/ranking/
        // redundancy from scratch.
        var computation = prefetchedComputation ?? ComputeCandidates(series, prefetchedVideoLookup, includeVariations,
            bypassEligibilityGate: includeTracks, preferredCandidateKey: preferredCandidateKey);
        if (computation is not { } value)
            return null;

        var (videoLookup, places, ranked, redundantPlaces) = value;

        var placeEpisodeCoverage = places.ToDictionary(
            p => p.ID,
            p => autoManagement.GetFileEpisodeCoverage(p, videoLookup));

        // When including tracks we need episode IDs for all places (including partial-coverage
        // groups not represented in any full candidate). Otherwise the candidates alone suffice.
        IEnumerable<int> episodeIdSource = includeTracks
            ? placeEpisodeCoverage.Values.SelectMany(cov => cov.Select(e => e.Item2))
            : ranked.SelectMany(c => c.EpisodeCoverage).Select(e => e.EpisodeID);

        var episodeLookup = episodeIdSource
            .Distinct()
            .Select(id => anidbEpisodes.GetByEpisodeID(id))
            .WhereNotNull()
            .ToDictionary(e => e.EpisodeID, e => (e.EpisodeType, e.EpisodeNumber));

        // Compute which signals actually vary across candidates so names only include
        // the qualifiers that distinguish one candidate from another.
        var candidateIncludeResolution = ranked.Select(c => c.Resolution).Where(r => r is not null).Distinct().Count() > 1;
        var candidateIncludeSource = ranked.Select(c => c.Source).Where(s => s != ReleaseSource.Unknown).Distinct().Count() > 1;
        var candidateIncludeVersion = ranked
            .Where(c => !string.IsNullOrEmpty(c.GroupID) && !string.IsNullOrEmpty(c.GroupSource))
            .GroupBy(c => $"{c.GroupID}|{c.GroupSource}")
            .Any(g => g.Count() > 1);

        // Pre-compute candidate names and detect collisions. When two candidates
        // would render with the same name (e.g., two unrecognised files from
        // different folders both named "Unknown"), append the first file's parent
        // folder (or filename when folders also match) as a disambiguation suffix.
        var precomputedNames = ranked
            .Select(c => ReleaseCandidate.ComputeName(c, candidateIncludeResolution, candidateIncludeSource, candidateIncludeVersion, episodeLookup))
            .ToList();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < precomputedNames.Count; i++)
        {
            var name = precomputedNames[i];
            if (!usedNames.Add(name))
            {
                // Find the first place's relative path and extract a unique suffix.
                var firstPath = ranked[i].Places.FirstOrDefault()?.RelativePath ?? string.Empty;
                var suffix = Path.GetFileName(Path.GetDirectoryName(firstPath));
                if (string.IsNullOrEmpty(suffix))
                    suffix = Path.GetFileNameWithoutExtension(firstPath);
                if (string.IsNullOrEmpty(suffix))
                    suffix = $"#{i + 1}";
                precomputedNames[i] = $"{name} ({suffix})";
                usedNames.Add(precomputedNames[i]);
            }
        }

        var candidateDTOs = new List<ReleaseCandidate>(ranked.Count);
        for (var i = 0; i < ranked.Count; i++)
        {
            var candidate = ranked[i];
            var isRedundant = candidate.Places.Count > 0 && candidate.Places.All(p => redundantPlaces.Contains(p.ID));

            ReleaseComparisonService.CompareDecision? decision = null;
            if (i > 0)
                decision = comparer.CompareWithDecision(ranked[0], candidate);

            candidateDTOs.Add(ReleaseCandidate.FromCandidate(
                candidate, rank: i + 1, isRedundant, videoLookup, redundantPlaces, decision,
                placeEpisodeCoverage, episodeLookup,
                candidateIncludeResolution, candidateIncludeSource, candidateIncludeVersion,
                nameOverride: precomputedNames[i]));
        }

        IReadOnlyList<ReleaseOverride> overrideDTOs = [];
        if (includeTracks)
        {
            var releaseOverrides = grouper.GetOverrides(places);
            var overrideIncludeResolution = releaseOverrides.Select(o => o.Resolution).Where(r => r is not null).Distinct().Count() > 1;
            var overrideIncludeSource = releaseOverrides.Select(o => o.Source).Where(s => s != ReleaseSource.Unknown).Distinct().Count() > 1;
            overrideDTOs = releaseOverrides
                .Select(o => ReleaseOverride.FromOverride(o, videoLookup, episodeLookup, overrideIncludeResolution, overrideIncludeSource))
                .ToList();
        }

        var filesToAutoDeleteCount = redundantPlaces.Count;

        return new SeriesWithCandidates
        {
            SeriesID = series.AnimeSeriesID,
            SeriesTitle = series.Title,
            AnidbAnimeID = series.AniDB_ID,
            IsAiring = autoManagement.IsSeriesAiring(series),
            HasRedundantCandidates = candidateDTOs.Any(c => c.IsRedundant),
            FilesToAutoDeleteCount = filesToAutoDeleteCount,
            Candidates = candidateDTOs,
            Overrides = overrideDTOs,
        };
    }

    private ReleaseDeletionPreview? ComputeSeriesPreview(
        AnimeSeries series,
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

        // An explicit per-series override means the user deliberately picked which candidate
        // to treat as primary — that deliberate choice bypasses the automatic-redundancy
        // eligibility gate (which exists to protect *unattended* decisions), even when the
        // chosen candidate is a mixed/gap-fill composite that could never qualify on its own.
        var isOverridden = overrides.TryGetValue(series.AnimeSeriesID, out var preferredKey)
                            && ranked.Any(c => c.Key == preferredKey);
        if (isOverridden)
        {
            var preferredIdx = ranked.FindIndex(c => c.Key == preferredKey);
            if (preferredIdx > 0)
            {
                var preferred = ranked[preferredIdx];
                ranked.RemoveAt(preferredIdx);
                ranked.Insert(0, preferred);
            }
        }
        var redundantPlaces = autoManagement.ComputeRedundantPlaces(series, ranked, videoLookup, bypassEligibilityGate: isOverridden);
        if (redundantPlaces.Count == 0)
            return null;

        // ComputeRedundantPlaces is already the authoritative source for which places are
        // redundant (whole-candidate or per-file, per settings) — use it directly. Composite/
        // gap-fill candidates commonly share most of their files with the primary and differ
        // in only one or two episodes; requiring every place in a candidate to be redundant
        // before counting any of them would incorrectly hide those per-file differences.
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
            .OrderBy(f => f.AbsolutePath)
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
