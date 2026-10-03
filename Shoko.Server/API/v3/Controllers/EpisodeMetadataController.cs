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
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// A Shoko episode's links to any metadata source: the linked episodes and
/// movies.
/// </summary>
/// <remarks>
/// <c>{source}</c> is a registered source's value, an alias or an old spelling,
/// ignoring case; AniDB and the server's own sources answer <c>400</c>, as
/// nothing is linked to them. Reading is open to every user who may see the
/// series; linking and unlinking is for admins. Links to episodes are changed
/// through the series' <c>CrossReferences/Episode</c> routes.
/// </remarks>
[ApiController]
[Route("/api/v{version:apiVersion}/Episode/{episodeID:int}/Metadata/{source:metadata-source}")]
[ApiV3]
[Authorize]
[NotSupportedAsBadRequest]
public class EpisodeMetadataController : ShokoMetadataControllerBase
{
    #region Fields

    private readonly IMetadataService _metadataService;

    private readonly IMetadataLinkingService _linkingService;

    private readonly MetadataModelBuilder _models;

    #endregion

    #region Constructors

    /// <summary>
    /// Takes the services the routes work through.
    /// </summary>
    /// <param name="settingsProvider">The settings.</param>
    /// <param name="logger">Logs refreshes refused while a source is paused.</param>
    /// <param name="userService">Tells who is asking.</param>
    /// <param name="metadataService">Reads the episode, the links and the linked entries.</param>
    /// <param name="linkingService">Makes and breaks the links.</param>
    /// <param name="refreshService">Refreshes what is linked.</param>
    /// <param name="models">Builds the models sent.</param>
    public EpisodeMetadataController(
        ISettingsProvider settingsProvider,
        ILogger<EpisodeMetadataController> logger,
        IUserService userService,
        IMetadataService metadataService,
        IMetadataLinkingService linkingService,
        IMetadataRefreshService refreshService,
        MetadataModelBuilder models
    ) : base(settingsProvider, logger, userService, metadataService, refreshService)
    {
        _metadataService = metadataService;
        _linkingService = linkingService;
        _models = models;
    }

    #endregion

    #region Episode

    /// <summary>
    /// Get the episodes of a source an episode is linked to.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The linked episodes that are stored, in the links' order.</returns>
    [HttpGet("Episode")]
    public async Task<ActionResult<List<MetadataEpisode>>> GetLinkedEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] MetadataSource source,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetEpisode(episodeID, source, out var error) is not { } episode)
            return error!;

        List<MetadataEpisode> models = [];
        var linkedIDs = _metadataService.GetEpisodeCrossReferences(episode.AnidbEpisodeID, source)
            .OrderBy(link => link.Ordering)
            .Select(link => link.ProviderID)
            .OfType<MetadataGuid>()
            .Distinct();
        foreach (var linkedID in linkedIDs)
            if (_metadataService.GetEntry<IEpisode>(linkedID) is { } linked && await Fresh<IEpisode>(linkedID, linked.SeriesID, cancellationToken).ConfigureAwait(false) is { } fresh)
                models.Add(_models.Episode(fresh, include));

        return models;
    }

    /// <summary>
    /// Get an episode's episode and movie links to a source.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <returns>The links.</returns>
    [HttpGet("CrossReferences")]
    public ActionResult<IReadOnlyList<MetadataCrossReference>> GetCrossReferences([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromRoute] MetadataSource source)
    {
        if (GetEpisode(episodeID, source, out var error) is not { } episode)
            return error!;

        return MetadataModelBuilder.CrossReferences(
            [
                .. _metadataService.GetEpisodeCrossReferences(episode.AnidbEpisodeID, source),
                .. _metadataService.GetMovieCrossReferences(episode.AnidbEpisodeID, source),
            ],
            _metadataService
        ).ToList();
    }

    #endregion

    #region Movie

    /// <summary>
    /// Get the movies of a source an episode is linked to.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The linked movies that are stored.</returns>
    [HttpGet("Movie")]
    public async Task<ActionResult<List<MetadataMovie>>> GetLinkedMovies(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] MetadataSource source,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetEpisode(episodeID, source, out var error) is not { } episode)
            return error!;

        List<MetadataMovie> models = [];
        var movieIDs = _metadataService.GetMovieCrossReferences(episode.AnidbEpisodeID, source)
            .Select(link => link.ProviderID)
            .OfType<MetadataGuid>()
            .Distinct();
        foreach (var movieID in movieIDs)
            if (await Fresh<IMovie>(movieID, movieID, cancellationToken).ConfigureAwait(false) is { } movie)
                models.Add(_models.Movie(movie, include));

        return models;
    }

    /// <summary>
    /// Link an episode to a movie of a source. The movie is refreshed when it
    /// never was in full, or when asked.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The movie to link. Its <c>EpisodeID</c> is ignored.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpPost("Movie")]
    public async Task<ActionResult> LinkMovie(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataLinkMovieBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (GetEpisode(episodeID, source, out var error) is not { } episode)
            return error!;

        if (MetadataEntryController.FromBody(source, MetadataEntityType.Movie, body.ID) is not { } movieID)
            return ValidationProblem($"'{body.ID}' is not a movie of {source.Name}.", nameof(body.ID));

        // The store has the last word on what may be linked, such as a TMDB 0.
        try
        {
            await _linkingService.AddMovieLink(new()
            {
                Source = source,
                EntityType = MetadataEntityType.Movie,
                ProviderID = movieID,
                AnidbEpisodeID = episode.AnidbEpisodeID,
                AnidbAnimeID = episode.AnidbEpisode.AnidbAnimeID,
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
    /// Unlink an episode from one movie of a source, or from every one, and
    /// keep the source from linking it again on its own.
    /// </summary>
    /// <param name="episodeID">The Shoko episode ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="body">The movie to unlink, or nothing for every one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Movie")]
    public async Task<ActionResult> UnlinkMovies(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataUnlinkBody? body = null,
        CancellationToken cancellationToken = default
    )
    {
        if (GetEpisode(episodeID, source, out var error) is not { } episode)
            return error!;

        if (!string.IsNullOrEmpty(body?.ID))
        {
            if (MetadataEntryController.FromBody(source, MetadataEntityType.Movie, body.ID) is not { } movieID)
                return ValidationProblem($"'{body.ID}' is not a movie of {source.Name}.", nameof(body.ID));

            await _linkingService.RemoveMovieLink(new()
            {
                Source = source,
                EntityType = MetadataEntityType.Movie,
                ProviderID = movieID,
                AnidbEpisodeID = episode.AnidbEpisodeID,
                AnidbAnimeID = episode.AnidbEpisode.AnidbAnimeID,
                Purge = body.Purge,
                DisableAutoLinking = true,
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _linkingService.RemoveLinksForEpisode(source, episode.AnidbEpisodeID, MetadataEntityType.Movie, body?.Purge ?? false, disableAutoLinking: true, cancellationToken)
                .ConfigureAwait(false);
        }

        return NoContent();
    }

    #endregion
}
