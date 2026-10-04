using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// A Shoko series' links to any metadata source: the linked series and
/// movies, the episode links, automatic searches and matching, and
/// refreshing what is linked.
/// </summary>
/// <remarks>
/// <c>{source}</c> is a registered source's value, an alias or an old spelling,
/// ignoring case; AniDB and the server's own sources answer <c>400</c>, as
/// nothing is linked to them. IDs in bodies and queries are the source's own or
/// full identifiers such as <c>anilist://series/21</c>. Reading is open to every
/// user who may see the series; changes, searches and refreshes are for admins.
/// </remarks>
[ApiController]
[Route("/api/v{version:apiVersion}/Series/{seriesID:int}/Metadata/{source:metadata-source}")]
[ApiV3]
[Authorize]
[NotSupportedAsBadRequest]
public class SeriesMetadataController : ShokoMetadataControllerBase
{
    #region Fields

    private readonly ILogger<SeriesMetadataController> _logger;

    private readonly IMetadataService _metadataService;

    private readonly IMetadataLinkingService _linkingService;

    private readonly IMetadataRefreshService _refreshService;

    private readonly IMetadataProviderManager _providerManager;

    private readonly MetadataModelBuilder _models;

    #endregion

    #region Constructors

    /// <summary>
    /// Takes the services the routes work through.
    /// </summary>
    /// <param name="settingsProvider">The settings.</param>
    /// <param name="logger">Logs matches that could not be made.</param>
    /// <param name="userService">Tells who is asking.</param>
    /// <param name="metadataService">Reads the series, the links and the linked entries.</param>
    /// <param name="linkingService">Makes and breaks the links.</param>
    /// <param name="refreshService">Refreshes what is linked.</param>
    /// <param name="providerManager">Tells what a source's providers can do.</param>
    /// <param name="models">Builds the models sent.</param>
    public SeriesMetadataController(
        ISettingsProvider settingsProvider,
        ILogger<SeriesMetadataController> logger,
        IUserService userService,
        IMetadataService metadataService,
        IMetadataLinkingService linkingService,
        IMetadataRefreshService refreshService,
        IMetadataProviderManager providerManager,
        MetadataModelBuilder models
    ) : base(settingsProvider, logger, userService, metadataService, refreshService)
    {
        _logger = logger;
        _metadataService = metadataService;
        _linkingService = linkingService;
        _refreshService = refreshService;
        _providerManager = providerManager;
        _models = models;
    }

    #endregion

    #region Constants

    internal const string NoLinkedSeries = "Unable to find an existing cross-reference for the series to use. Make sure at least one series of the source is linked to the Shoko Series.";

    internal const string ParentNotLinked = "Unable to find an existing cross-reference for the given series. Please first link the series to the Shoko Series.";

    #endregion

    #region Auto-Search

    /// <summary>
    /// Search for the entries of a source a series is, and return every
    /// candidate the search scored without linking anything, the ones an
    /// automatic search would not link saying why.
    /// </summary>
    /// <remarks>
    /// A candidate turned down is linked by hand through the link routes.
    /// </remarks>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>
    /// The candidates, best first, or <c>503 Service Unavailable</c> while
    /// the source's auto-linker is paused or not configured.
    /// </returns>
    [Authorize("admin")]
    [HttpGet("Action/AutoSearch")]
    public async Task<ActionResult<List<MetadataAutoMatchResult>>> PreviewAutoSearch(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (_providerManager.MetadataProviders.FirstOrDefault(info => info.Source == source && info.Enabled && info.IsAutoLinker) is not { } autoLinker)
            return ValidationProblem($"No enabled provider auto-links {source.Name}.", "source");

        if (MetadataPauseResponses.Refuse(Response, source, autoLinker, _refreshService.GetPauseStatus(source)) is { } refused)
            return refused;

        var candidates = await _linkingService.PreviewAutoLink(source, series.AnidbAnimeID, cancellationToken).ConfigureAwait(false);
        return candidates
            .Select(candidate => new MetadataAutoMatchResult(
                candidate,
                _metadataService.GetEntry(candidate.ID) is not null,
                _metadataService.GetSiteUrl(candidate.ID)
            ))
            .ToList();
    }

    /// <summary>
    /// Queue a search for the entries of a source a series is, which links
    /// the best match.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="force">
    /// Search even when the series is linked already or left alone, and
    /// replace every link it has on the source, verified ones and episode
    /// links included, with what is taken.
    /// </param>
    /// <param name="cancellationToken">Cancels the queueing.</param>
    /// <returns>
    /// No content, or <c>503 Service Unavailable</c> while the source's
    /// auto-linker is not configured.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Action/AutoSearch")]
    public async Task<ActionResult> ScheduleAutoSearch(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (_providerManager.MetadataProviders.FirstOrDefault(info => info.Source == source && info.IsAutoLinker) is { Provider.IsConfigured: false } unconfigured)
            return MetadataPauseResponses.NotConfigured(unconfigured);

        await _refreshService.AutoSearch(source, series.AnidbAnimeID, force, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Get whether a source may link a series on its own.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <returns>The state.</returns>
    [HttpGet("AutoLinking")]
    public ActionResult<MetadataAutoLinkingState> GetAutoLinking([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromRoute] MetadataSource source)
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        return new MetadataAutoLinkingState { Source = source, Disabled = _linkingService.IsAutoLinkingDisabled(series, source) };
    }

    /// <summary>
    /// Set whether a source may link a series on its own.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The state to set.</param>
    /// <returns>The state.</returns>
    [Authorize("admin")]
    [HttpPut("AutoLinking")]
    public ActionResult<MetadataAutoLinkingState> SetAutoLinking(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataAutoLinkingBody body
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        _linkingService.SetAutoLinkingDisabled(series, source, body.Disabled);
        return new MetadataAutoLinkingState { Source = source, Disabled = _linkingService.IsAutoLinkingDisabled(series, source) };
    }

    #endregion

    #region Series

    /// <summary>
    /// Get the series of a source a series is linked to.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The linked series that are stored.</returns>
    [HttpGet("Series")]
    public async Task<ActionResult<List<MetadataSeries>>> GetLinkedSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        List<MetadataSeries> models = [];
        foreach (var linkedID in LinkedSeries(series, source))
            if (await Fresh<ISeries>(linkedID, linkedID, cancellationToken).ConfigureAwait(false) is { } linked)
                models.Add(_models.Series(linked, include));

        return models;
    }

    /// <summary>
    /// Link a series to a series of a source and match its episodes. The
    /// linked series is refreshed when it never was in full, or when asked.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The series to link.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpPost("Series")]
    public async Task<ActionResult> LinkSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataLinkSeriesBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (MetadataEntryController.FromBody(source, MetadataEntityType.Series, body.ID) is not { } linkedID)
            return ValidationProblem($"'{body.ID}' is not a series of {source.Name}.", nameof(body.ID));

        // The store has the last word on what may be linked, such as a TMDB 0.
        try
        {
            await AddSeriesLink(series, linkedID, additive: !body.Replace, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.ID));
        }

        await RefreshLinked(linkedID, body.Refresh, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Unlink a series from one series of a source, or from every one, and
    /// keep the source from linking it again on its own.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The series to unlink, or nothing for every one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Series")]
    public async Task<ActionResult> UnlinkSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataUnlinkBody? body = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (!string.IsNullOrEmpty(body?.ID))
        {
            if (MetadataEntryController.FromBody(source, MetadataEntityType.Series, body.ID) is not { } linkedID)
                return ValidationProblem($"'{body.ID}' is not a series of {source.Name}.", nameof(body.ID));

            await _linkingService.RemoveSeriesLink(new()
            {
                Source = source,
                EntityType = MetadataEntityType.Series,
                ProviderID = linkedID,
                AnidbAnimeID = series.AnidbAnimeID,
                Purge = body.Purge,
                DisableAutoLinking = true,
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _linkingService.RemoveLinksForAnime(source, series.AnidbAnimeID, MetadataEntityType.Series, body?.Purge ?? false, disableAutoLinking: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return NoContent();
    }

    #endregion

    #region Movie

    /// <summary>
    /// Get the movies of a source a series' episodes are linked to.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The linked movies that are stored.</returns>
    [HttpGet("Movie")]
    public async Task<ActionResult<List<MetadataMovie>>> GetLinkedMovies(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        List<MetadataMovie> models = [];
        foreach (var movieID in LinkedMovies(series, source))
            if (await Fresh<IMovie>(movieID, movieID, cancellationToken).ConfigureAwait(false) is { } movie)
                models.Add(_models.Movie(movie, include));

        return models;
    }

    /// <summary>
    /// Link an AniDB episode of a series to a movie of a source. The movie is
    /// refreshed when it never was in full, or when asked.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The movie, and the episode standing for it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpPost("Movie")]
    public async Task<ActionResult> LinkMovie(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataLinkMovieBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (MetadataEntryController.FromBody(source, MetadataEntityType.Movie, body.ID) is not { } movieID)
            return ValidationProblem($"'{body.ID}' is not a movie of {source.Name}.", nameof(body.ID));

        var anidbEpisodes = series.AnidbAnime.Episodes;
        var anidbEpisode = body.EpisodeID is { } episodeID
            ? anidbEpisodes.FirstOrDefault(episode => episode.AnidbID == episodeID)
            : anidbEpisodes.Where(episode => episode.Type is EpisodeType.Episode).OrderBy(episode => episode.EpisodeNumber).FirstOrDefault();
        if (anidbEpisode is null)
            return ValidationProblem(body.EpisodeID is null ? "The series has no regular episode to stand for the movie." : "The episode is not part of the series.", nameof(body.EpisodeID));

        // The store has the last word on what may be linked, such as a TMDB 0.
        try
        {
            await _linkingService.AddMovieLink(new()
            {
                Source = source,
                EntityType = MetadataEntityType.Movie,
                ProviderID = movieID,
                AnidbEpisodeID = anidbEpisode.AnidbID,
                AnidbAnimeID = series.AnidbAnimeID,
                Additive = !body.Replace,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message, nameof(body.ID));
        }

        await RefreshLinked(movieID, body.Refresh, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Unlink a series' AniDB episodes from one movie of a source, or from
    /// every one, and keep the source from linking them again on its own.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The movie and episode to unlink; every one when left out.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Movie")]
    public async Task<ActionResult> UnlinkMovies(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataUnlinkMovieBody? body = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        var episodeIDs = series.AnidbAnime.Episodes.Select(episode => episode.AnidbID).ToList();
        if (body?.EpisodeID is { } episodeID)
        {
            if (!episodeIDs.Contains(episodeID))
                return ValidationProblem("The episode is not part of the series.", nameof(body.EpisodeID));

            episodeIDs = [episodeID];
        }

        MetadataGuid? movieID = null;
        if (!string.IsNullOrEmpty(body?.ID) && (movieID = MetadataEntryController.FromBody(source, MetadataEntityType.Movie, body.ID)) is null)
            return ValidationProblem($"'{body.ID}' is not a movie of {source.Name}.", nameof(body.ID));

        foreach (var anidbEpisodeID in episodeIDs)
        {
            if (movieID is not null)
                await _linkingService.RemoveMovieLink(new()
                {
                    Source = source,
                    EntityType = MetadataEntityType.Movie,
                    ProviderID = movieID,
                    AnidbEpisodeID = anidbEpisodeID,
                    AnidbAnimeID = series.AnidbAnimeID,
                    Purge = body!.Purge,
                    DisableAutoLinking = true,
                }, cancellationToken).ConfigureAwait(false);
            else
                await _linkingService.RemoveLinksForEpisode(source, anidbEpisodeID, MetadataEntityType.Movie, body?.Purge ?? false, disableAutoLinking: true, cancellationToken)
                    .ConfigureAwait(false);
        }

        return NoContent();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh every series and movie of a source a series is linked to.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">How to refresh them.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>200 when they ran, 204 when they were queued, or 503 while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Action/Refresh")]
    public async Task<ActionResult> RefreshLinked(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataRefreshBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        var quick = body.Immediate && body.QuickRefresh;
        var force = !quick && body.Force;
        var options = body.ToOptions(quick);
        var entries = LinkedSeries(series, source).Concat(LinkedMovies(series, source))
            .Where(entryID => !body.SkipIfExists || _metadataService.GetEntry(entryID) is null)
            .ToList();
        if (await QueueWhenPaused(source, () => ForEach(entries, entryID => _refreshService.RefreshEntry(entryID, force, options, prioritize: true, cancellationToken: cancellationToken)), "A refresh", body.Immediate).ConfigureAwait(false) is { } paused)
            return paused;

        await ForEach(entries, entryID => _refreshService.RefreshEntry(entryID, force, options, immediate: body.Immediate, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return body.Immediate ? Ok() : NoContent();
    }

    /// <summary>
    /// Download the images of every series and movie of a source a series is
    /// linked to.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">How to download them.</param>
    /// <param name="cancellationToken">Cancels a download waited on.</param>
    /// <returns>200 when they ran, 204 when they were queued, or 503 while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Action/DownloadImages")]
    public async Task<ActionResult> DownloadLinkedImages(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataDownloadImagesBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        var entries = LinkedSeries(series, source).Concat(LinkedMovies(series, source)).ToList();
        if (await QueueWhenPaused(source, () => ForEach(entries, entryID => _refreshService.DownloadImages(entryID, body.Force, prioritize: true, cancellationToken: cancellationToken)), "An image download", body.Immediate).ConfigureAwait(false) is { } paused)
            return paused;

        await ForEach(entries, entryID => _refreshService.DownloadImages(entryID, body.Force, immediate: body.Immediate, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return body.Immediate ? Ok() : NoContent();
    }

    #endregion

    #region Cross-References

    /// <summary>
    /// Get a series' series and movie links to a source.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <returns>The links.</returns>
    [HttpGet("CrossReferences")]
    public ActionResult<IReadOnlyList<MetadataCrossReference>> GetCrossReferences([FromRoute, Range(1, int.MaxValue)] int seriesID, [FromRoute] MetadataSource source)
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        return MetadataModelBuilder.CrossReferences(
            [
                .. _metadataService.GetSeriesCrossReferences(series.AnidbAnimeID, source),
                .. _metadataService.GetMovieCrossReferencesForSeries(series.AnidbAnimeID, source),
            ],
            _metadataService
        ).ToList();
    }

    /// <summary>
    /// Get a series' episode links to a source, optionally only the ones into
    /// one linked series.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="parentID">Only the links into this linked series, and the links to nothing.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page of links.</returns>
    [HttpGet("CrossReferences/Episode")]
    public ActionResult<ListResult<MetadataCrossReference>> GetEpisodeCrossReferences(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery] string? parentID = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (EpisodeLinks(series, source, parentID, out var problem) is not { } links)
            return problem!;

        return links.ToListResult(link => MetadataModelBuilder.CrossReference(link, _metadataService), page, pageSize);
    }

    /// <summary>
    /// Get a series' episode links to a source in groups: an AniDB episode
    /// linked to several episodes is one group, and so are the AniDB episodes
    /// sharing one episode.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="parentID">Only the links into this linked series, and the links to nothing.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page of groups.</returns>
    [HttpGet("CrossReferences/EpisodeGroups")]
    public ActionResult<ListResult<List<MetadataCrossReference>>> GetEpisodeCrossReferenceGroups(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery] string? parentID = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (EpisodeLinks(series, source, parentID, out var problem) is not { } links)
            return problem!;

        return MetadataEntryController.GroupEpisodeLinks(links, _metadataService)
            .ToListResult(group => group.Select(link => MetadataModelBuilder.CrossReference(link, _metadataService)).ToList(), page, pageSize);
    }

    /// <summary>
    /// Change a series' episode links to a source by hand: clear them, and
    /// replace, add or point AniDB episodes at nothing. A series of the
    /// source an episode belongs to is linked first when it is not yet.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">What to change.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 when a newly linked series had to be refreshed, otherwise no content.</returns>
    [Authorize("admin")]
    [HttpPost("CrossReferences/Episode")]
    public async Task<ActionResult> OverrideEpisodeCrossReferences(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataOverrideEpisodeLinksBody body,
        CancellationToken cancellationToken = default
    )
    {
        if (body is null || (body.Mapping.Count is 0 && !body.UnsetAll))
            return ValidationProblem("Empty body.");

        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        var linked = LinkedSeries(series, source).ToHashSet();
        var missing = new HashSet<MetadataGuid>();
        var mapping = new List<(MetadataOverrideEpisodeLinkBody Link, IShokoEpisode Episode, IEpisode? Linked)>();
        foreach (var link in body.Mapping.DistinctBy(link => (link.AniDBID, link.IsEmpty ? null : link.ID)))
        {
            if (_metadataService.GetShokoEpisodeByAnidbID(link.AniDBID) is not { } episode)
            {
                ModelState.AddModelError(nameof(body.Mapping), $"Unable to find an AniDB Episode with id '{link.AniDBID}'");
                continue;
            }

            if (episode.ShokoSeriesID != seriesID)
            {
                ModelState.AddModelError(nameof(body.Mapping), $"The AniDB Episode with id '{link.AniDBID}' is not part of the series.");
                continue;
            }

            IEpisode? linkedEpisode = null;
            if (!link.IsEmpty)
            {
                if (MetadataEntryController.FromBody(source, MetadataEntityType.Episode, link.ID) is not { } episodeID ||
                    _metadataService.GetEntry<IEpisode>(episodeID) is not { } found)
                {
                    ModelState.AddModelError(nameof(body.Mapping), $"Unable to find {source.Name} Episode with the id '{link.ID}' locally.");
                    continue;
                }

                linkedEpisode = found;
                if (!linked.Contains(found.SeriesID))
                    missing.Add(found.SeriesID);
            }

            mapping.Add((link, episode, linkedEpisode));
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        foreach (var parentID in missing)
            await AddSeriesLink(series, parentID, additive: true, cancellationToken).ConfigureAwait(false);

        if (body.UnsetAll)
            await _linkingService.ResetEpisodeLinks(source, series.AnidbAnimeID, allowAutoMatch: false, cancellationToken).ConfigureAwait(false);

        // Replacing links go first, so the links added beside them stay.
        foreach (var (link, _, linkedEpisode) in mapping
            .OrderByDescending(pair => pair.Link.Replace)
            .ThenBy(pair => pair.Episode.AnidbEpisode.Type)
            .ThenBy(pair => pair.Episode.AnidbEpisode.EpisodeNumber))
            await _linkingService.SetEpisodeLink(
                source,
                link.AniDBID,
                linkedEpisode?.ID,
                additive: linkedEpisode is not null && !link.Replace,
                ordering: link.Index,
                providerSeriesID: linkedEpisode?.SeriesID,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

        var scheduled = false;
        foreach (var parentID in missing)
            scheduled |= await RefreshLinked(parentID, false, cancellationToken).ConfigureAwait(false);

        return scheduled ? Created() : NoContent();
    }

    /// <summary>
    /// Preview how a series' episodes would be matched against a series of a
    /// source, without writing anything.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="parentID">The series to match against; the first linked one when left out.</param>
    /// <param name="seasonID">Only match against this season of it.</param>
    /// <param name="keepExisting">Keep the links already there.</param>
    /// <param name="considerExistingOtherLinks">Leave out the episodes other anime claim; left out, the settings decide.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Cancels the match.</param>
    /// <returns>The page of links the match would leave.</returns>
    [Authorize("admin")]
    [HttpGet("CrossReferences/Episode/Auto")]
    public async Task<ActionResult<ListResult<MetadataCrossReference>>> PreviewAutoMatchEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery] string? parentID = null,
        [FromQuery] string? seasonID = null,
        [FromQuery] bool keepExisting = true,
        [FromQuery] bool? considerExistingOtherLinks = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (MatchTarget(series, source, parentID, seasonID, out var problem) is not { } target)
            return problem!;

        var links = await _linkingService.MatchEpisodes(
            series.AnidbAnimeID,
            target.Parent,
            target.Season,
            useExisting: keepExisting,
            save: false,
            considerOtherLinks: considerExistingOtherLinks,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
        return links.ToListResult(link => MetadataModelBuilder.CrossReference(link, _metadataService), page, pageSize);
    }

    /// <summary>
    /// Match a series' episodes against a series of a source and write the
    /// result, linking that series first when it is not yet.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">How to match; the first linked series, keeping the links there, when left out.</param>
    /// <param name="cancellationToken">Cancels the match.</param>
    /// <returns>201 when the series had to be refreshed, otherwise no content.</returns>
    [Authorize("admin")]
    [HttpPost("CrossReferences/Episode/Auto")]
    public async Task<ActionResult> AutoMatchEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataAutoMatchEpisodesBody? body = null,
        CancellationToken cancellationToken = default
    )
    {
        body ??= new();
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        if (MatchTarget(series, source, body.ParentID, body.SeasonID, out var problem) is not { } target)
            return problem!;

        if (_metadataService.GetEntry<ISeries>(target.Parent) is null)
            return ValidationProblem($"Unable to find the selected {source.Name} series locally. Add the series locally first.", nameof(body.ParentID));

        if (!LinkedSeries(series, source).Contains(target.Parent))
            await AddSeriesLink(series, target.Parent, additive: true, cancellationToken).ConfigureAwait(false);
        else
            await _linkingService.MatchEpisodes(
                series.AnidbAnimeID,
                target.Parent,
                target.Season,
                useExisting: body.KeepExisting,
                save: true,
                considerOtherLinks: body.ConsiderExistingOtherLinks,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

        return await RefreshLinked(target.Parent, false, cancellationToken).ConfigureAwait(false) ? Created() : NoContent();
    }

    /// <summary>
    /// Reset a series' episode links to a source, leaving them for automatic
    /// matching to fill in again.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("CrossReferences/Episode")]
    public async Task<ActionResult> ResetEpisodeCrossReferences(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        await _linkingService.ResetEpisodeLinks(source, series.AnidbAnimeID, allowAutoMatch: true, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Season

    /// <summary>
    /// Get the seasons of a source a series' episodes are linked into.
    /// </summary>
    /// <param name="seriesID">The Shoko series ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The seasons that are stored, by series and number.</returns>
    [HttpGet("Season")]
    public async Task<ActionResult<List<MetadataSeason>>> GetLinkedSeasons(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromRoute] MetadataSource source,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetSeries(seriesID, source, out var error) is not { } series)
            return error!;

        var seasonIDs = _metadataService.GetEpisodeCrossReferencesForSeries(series.AnidbAnimeID, source)
            .Select(link => link.ProviderID is { } episodeID ? _metadataService.GetEntry<IEpisode>(episodeID)?.SeasonID : null)
            .OfType<MetadataGuid>()
            .Distinct()
            .ToList();
        List<ISeason> seasons = [];
        foreach (var seasonID in seasonIDs)
            if (_metadataService.GetEntry<ISeason>(seasonID) is { } season && await Fresh<ISeason>(seasonID, season.SeriesID, cancellationToken).ConfigureAwait(false) is { } fresh)
                seasons.Add(fresh);

        return seasons
            .OrderBy(season => season.SeriesID.ID, StringComparer.Ordinal)
            .ThenBy(season => season.SeasonNumber)
            .Select(season => _models.Season(season, include))
            .ToList();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// The series of a source a series is linked to.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The linked series, once each.</returns>
    private IReadOnlyList<MetadataGuid> LinkedSeries(IShokoSeries series, MetadataSource source)
        => [.. _metadataService.GetSeriesCrossReferences(series.AnidbAnimeID, source)
            .Select(link => link.ProviderID)
            .OfType<MetadataGuid>()
            .Where(id => id.EntityType == MetadataEntityType.Series)
            .Distinct()];

    /// <summary>
    /// The movies of a source a series' episodes are linked to.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The linked movies, once each.</returns>
    private IReadOnlyList<MetadataGuid> LinkedMovies(IShokoSeries series, MetadataSource source)
        => [.. _metadataService.GetMovieCrossReferencesForSeries(series.AnidbAnimeID, source)
            .Select(link => link.ProviderID)
            .OfType<MetadataGuid>()
            .Distinct()];

    /// <summary>
    /// Links a series to a series of a source, which matches its episodes at
    /// once, as a series linked by hand always was.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="linkedID">The series to link.</param>
    /// <param name="additive">Whether to keep the series' other links to the source.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the link and its episodes are written.</returns>
    private async Task AddSeriesLink(IShokoSeries series, MetadataGuid linkedID, bool additive, CancellationToken cancellationToken)
    {
        await _linkingService.AddSeriesLink(new()
        {
            Source = linkedID.Source,
            EntityType = MetadataEntityType.Series,
            ProviderID = linkedID,
            AnidbAnimeID = series.AnidbAnimeID,
            Additive = additive,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A series' episode links to a source, optionally only the ones into one
    /// linked series and the ones to nothing.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source.</param>
    /// <param name="parentID">The linked series, if any.</param>
    /// <param name="problem">The answer to give when the linked series is not one.</param>
    /// <returns>The links, or <c>null</c> with <paramref name="problem"/> set.</returns>
    private IReadOnlyList<IMetadataEpisodeCrossReference>? EpisodeLinks(IShokoSeries series, MetadataSource source, string? parentID, out ActionResult? problem)
    {
        problem = null;
        var links = _metadataService.GetEpisodeCrossReferencesForSeries(series.AnidbAnimeID, source);
        if (string.IsNullOrEmpty(parentID))
            return links;

        if (MetadataEntryController.FromBody(source, MetadataEntityType.Series, parentID) is not { } parent || !LinkedSeries(series, source).Contains(parent))
        {
            problem = ValidationProblem(ParentNotLinked, nameof(parentID));
            return null;
        }

        return [.. links.Where(link => link.ProviderParentID == parent || link.ProviderID is null)];
    }

    /// <summary>
    /// The series and season of a source to match a series' episodes
    /// against.
    /// </summary>
    /// <param name="series">The Shoko series.</param>
    /// <param name="source">The source.</param>
    /// <param name="parentID">The series named, or <c>null</c> for the first linked one.</param>
    /// <param name="seasonID">The season named, if any.</param>
    /// <param name="problem">The answer to give when there is nothing to match against.</param>
    /// <returns>The series and season, or <c>null</c> with <paramref name="problem"/> set.</returns>
    private (MetadataGuid Parent, MetadataGuid? Season)? MatchTarget(IShokoSeries series, MetadataSource source, string? parentID, string? seasonID, out ActionResult? problem)
    {
        problem = null;
        MetadataGuid parent;
        if (string.IsNullOrEmpty(parentID))
        {
            if (LinkedSeries(series, source) is not [var first, ..])
            {
                problem = ValidationProblem(NoLinkedSeries, nameof(parentID));
                return null;
            }

            parent = first;
        }
        else if (MetadataEntryController.FromBody(source, MetadataEntityType.Series, parentID) is { } named)
        {
            parent = named;
        }
        else
        {
            problem = ValidationProblem($"'{parentID}' is not a series of {source.Name}.", nameof(parentID));
            return null;
        }

        if (string.IsNullOrEmpty(seasonID))
            return (parent, null);

        if (MetadataEntryController.FromBody(source, MetadataEntityType.Season, seasonID) is not { } season || _metadataService.GetEntry<ISeason>(season) is not { } stored)
        {
            problem = ValidationProblem($"Unable to find an existing {source.Name} season with the given season ID.", nameof(seasonID));
            return null;
        }

        if (stored.SeriesID != parent)
        {
            problem = ValidationProblem("The selected season does not belong to the selected series.", nameof(seasonID));
            return null;
        }

        return (parent, season);
    }

    #endregion
}
