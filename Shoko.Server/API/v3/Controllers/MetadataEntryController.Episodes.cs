using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.API.v3.Models.Shoko;

using File = Shoko.Server.API.v3.Models.Shoko.File;
using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Controllers;

public partial class MetadataEntryController
{
    #region Seasons

    /// <summary>
    /// Get a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The season.</returns>
    [HttpGet("Season/{id}")]
    public async Task<ActionResult<MetadataSeason>> GetSeasonByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? _models.Season(season, include, language)
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get every title of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The titles, the preferred one first.</returns>
    [HttpGet("Season/{id}/Titles")]
    public async Task<ActionResult<IReadOnlyList<Title>>> GetSeasonTitles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? Ok(_models.Titles(season.ID, language))
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get every overview of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Season/{id}/Overviews")]
    public async Task<ActionResult<IReadOnlyList<Overview>>> GetSeasonOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? Ok(_models.Overviews(season.ID, language))
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get every image of a stored season, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The images.</returns>
    [HttpGet("Season/{id}/Images")]
    public async Task<ActionResult<Images>> GetSeasonImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? _models.Images(season, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, language, includeRemoteUrl)
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the cast of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The cast.</returns>
    [HttpGet("Season/{id}/Cast")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetSeasonCast([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? season.Cast.Select(_models.Cast).ToList()
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the crew of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The crew.</returns>
    [HttpGet("Season/{id}/Crew")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetSeasonCrew([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? season.Crew.Select(_models.Crew).ToList()
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the yearly seasons a stored season aired in.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The yearly seasons.</returns>
    [HttpGet("Season/{id}/YearlySeasons")]
    public async Task<ActionResult<IReadOnlyList<SeasonWithYear>>> GetSeasonYearlySeasons([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? season.YearlySeasons.ToV3Dto()
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the days of the week a stored season aired episodes on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The days' names, in alphabetical order.</returns>
    [HttpGet("Season/{id}/DaysOfWeek")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSeasonDaysOfWeek([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? Ok(DaysOfWeek(season.Episodes))
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the series a stored season belongs to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The series.</returns>
    [HttpGet("Season/{id}/Series")]
    public async Task<ActionResult<MetadataSeries>> GetSeasonSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
    {
        if (await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is not { } season)
            return NotFound(SeasonNotFound);

        return season.Series is { } series ? _models.Series(series, include, language) : NotFound(SeriesNotFound);
    }

    /// <summary>
    /// Get the episodes of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page, by number.</returns>
    [HttpGet("Season/{id}/Episode")]
    public async Task<ActionResult<ListResult<MetadataEpisode>>> GetSeasonEpisodes(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? Page(InOrder(season.Episodes).Select(episode => _models.Episode(episode, include, language)), page, pageSize)
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the AniDB anime linked to the episodes of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The anime.</returns>
    [HttpGet("Season/{id}/AniDB/Anime")]
    public async Task<ActionResult<List<AnidbAnime>>> GetSeasonAnidbAnime([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? AnidbAnime(season.Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences))
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the Shoko series linked to the episodes of a stored season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="randomImages">Pick the series' images at random.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko series the user may see.</returns>
    [HttpGet("Season/{id}/Shoko/Series")]
    public async Task<ActionResult<List<Series>>> GetSeasonShokoSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? ShokoSeries(season.Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences).Select(link => link.AnidbAnimeID), randomImages, includeDataFrom)
            : NotFound(SeasonNotFound);

    /// <summary>
    /// Get the files of the Shoko episodes linked to the episodes of a stored
    /// season.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the season.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="include">Include items that are not included by default.</param>
    /// <param name="exclude">Exclude items of certain types.</param>
    /// <param name="include_only">Filter to only include items of certain types.</param>
    /// <param name="releaseProviders">Only files from these release providers; append <c>!</c> to a name to leave its files out.</param>
    /// <param name="sortOrder">Sort order. Put <c>-</c> before a criterion to reverse it.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of files.</returns>
    [HttpGet("Season/{id}/Shoko/File")]
    public async Task<ActionResult<ListResult<File>>> GetSeasonShokoFiles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeason>(source, MetadataEntityType.Season, id, cancellationToken).ConfigureAwait(false) is { } season
            ? ShokoFiles(season.Episodes.SelectMany(episode => episode.MetadataEpisodeCrossReferences).Select(link => link.AnidbEpisodeID), pageSize, page, include, exclude, include_only, releaseProviders, sortOrder)
            : NotFound(SeasonNotFound);

    #endregion

    #region Episodes

    #region Episodes | Basics

    /// <summary>
    /// Get several stored episodes of a source at once.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="body">The episodes and the extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The episodes that are stored, in the order asked for.</returns>
    [HttpPost("Episode/Bulk")]
    public async Task<ActionResult<List<MetadataEpisode>>> GetEpisodesInBulk(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkFetchBody body,
        CancellationToken cancellationToken = default
    )
    {
        List<MetadataEpisode> models = [];
        foreach (var guid in body.IDs.Select(id => FromBody(source, MetadataEntityType.Episode, id)).OfType<MetadataGuid>())
            if (await Get<IEpisode>(guid, cancellationToken).ConfigureAwait(false) is { } episode)
                models.Add(_models.Episode(episode, body.Include));

        return models;
    }

    /// <summary>
    /// Get a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The episode.</returns>
    [HttpGet("Episode/{id}")]
    public async Task<ActionResult<MetadataEpisode>> GetEpisodeByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? _models.Episode(episode, include, language)
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get every title of a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="includeSynthesized">Add the title made up for an episode with no title of its own, when that is its preferred title.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The titles, the preferred one first.</returns>
    [HttpGet("Episode/{id}/Titles")]
    public async Task<ActionResult<IReadOnlyList<Title>>> GetEpisodeTitles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] bool includeSynthesized = false,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? Ok(_models.Titles(episode.ID, language, includeSynthesized))
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get every overview of a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Episode/{id}/Overviews")]
    public async Task<ActionResult<IReadOnlyList<Overview>>> GetEpisodeOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? Ok(_models.Overviews(episode.ID, language))
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get every image of a stored episode, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The images.</returns>
    [HttpGet("Episode/{id}/Images")]
    public async Task<ActionResult<Images>> GetEpisodeImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? _models.Images(episode, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, language, includeRemoteUrl)
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the cast of a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The cast.</returns>
    [HttpGet("Episode/{id}/Cast")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetEpisodeCast([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? episode.Cast.Select(_models.Cast).ToList()
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the crew of a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The crew.</returns>
    [HttpGet("Episode/{id}/Crew")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetEpisodeCrew([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? episode.Crew.Select(_models.Crew).ToList()
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the external resources of a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The resources.</returns>
    [HttpGet("Episode/{id}/Resources")]
    public async Task<ActionResult<IReadOnlyList<Resource>>> GetEpisodeResources([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? Ok(MetadataModelBuilder.Resources(episode))
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get where a stored episode sits in each ordering of its series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The episode's place in each ordering.</returns>
    [HttpGet("Episode/{id}/Orderings")]
    public async Task<ActionResult<IReadOnlyList<MetadataEpisodeOrdering>>> GetEpisodeOrderings([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? _orderingService.GetEpisodeOrderings(episode).Select(MetadataModelBuilder.EpisodeOrdering).ToList()
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the links from AniDB episodes to a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The links, by AniDB episode.</returns>
    [HttpGet("Episode/{id}/CrossReferences")]
    public async Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> GetEpisodeCrossReferences([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? Ok(MetadataModelBuilder.CrossReferences(episode.MetadataEpisodeCrossReferences))
            : NotFound(EpisodeNotFound);

    #endregion

    #region Episodes | Parents

    /// <summary>
    /// Get the series a stored episode belongs to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The series.</returns>
    [HttpGet("Episode/{id}/Series")]
    public async Task<ActionResult<MetadataSeries>> GetEpisodeSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
    {
        if (await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.Series is { } series ? _models.Series(series, include, language) : NotFound(SeriesNotFound);
    }

    /// <summary>
    /// Get the season a stored episode belongs to.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The season.</returns>
    [HttpGet("Episode/{id}/Season")]
    public async Task<ActionResult<MetadataSeason>> GetEpisodeSeason(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
    {
        if (await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.SeasonID is { } seasonID && _metadataService.GetEntry<ISeason>(seasonID) is { } season
            ? _models.Season(season, include, language)
            : NotFound(SeasonNotFound);
    }

    #endregion

    #region Episodes | Reverse Lookups

    /// <summary>
    /// Get the AniDB episodes linked to a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The AniDB episodes.</returns>
    [HttpGet("Episode/{id}/AniDB/Episode")]
    public async Task<ActionResult<List<AnidbEpisode>>> GetEpisodeAnidbEpisodes([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? AnidbEpisodes(episode.MetadataEpisodeCrossReferences.Select(link => link.AnidbEpisodeID))
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the Shoko episodes linked to a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko episodes the user may see.</returns>
    [HttpGet("Episode/{id}/Shoko/Episode")]
    public async Task<ActionResult<List<Episode>>> GetEpisodeShokoEpisodes(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? ShokoEpisodes(episode.MetadataEpisodeCrossReferences.Select(link => link.AnidbEpisodeID), includeDataFrom)
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the Shoko series linked to a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="randomImages">Pick the series' images at random.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko series the user may see.</returns>
    [HttpGet("Episode/{id}/Shoko/Series")]
    public async Task<ActionResult<List<Series>>> GetEpisodeShokoSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? ShokoSeries(episode.MetadataEpisodeCrossReferences.Select(link => link.AnidbAnimeID), randomImages, includeDataFrom)
            : NotFound(EpisodeNotFound);

    /// <summary>
    /// Get the files of the Shoko episodes linked to a stored episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="include">Include items that are not included by default.</param>
    /// <param name="exclude">Exclude items of certain types.</param>
    /// <param name="include_only">Filter to only include items of certain types.</param>
    /// <param name="releaseProviders">Only files from these release providers; append <c>!</c> to a name to leave its files out.</param>
    /// <param name="sortOrder">Sort order. Put <c>-</c> before a criterion to reverse it.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of files.</returns>
    [HttpGet("Episode/{id}/Shoko/File")]
    public async Task<ActionResult<ListResult<File>>> GetEpisodeShokoFiles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IEpisode>(source, MetadataEntityType.Episode, id, cancellationToken).ConfigureAwait(false) is { } episode
            ? ShokoFiles(episode.MetadataEpisodeCrossReferences.Select(link => link.AnidbEpisodeID), pageSize, page, include, exclude, include_only, releaseProviders, sortOrder)
            : NotFound(EpisodeNotFound);

    #endregion

    #endregion
}
