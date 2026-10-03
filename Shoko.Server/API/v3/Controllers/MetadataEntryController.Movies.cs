using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    #region Movies

    #region Movies | Basics

    /// <summary>
    /// List the stored movies of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="search">Only the movies whose titles match this.</param>
    /// <param name="fuzzy">Whether to match titles loosely.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="restricted">Include, leave out, or only include the movies for adults.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page.</returns>
    [HttpGet("Movie")]
    public async Task<ActionResult<ListResult<MetadataMovie>>> GetMovies(
        [FromRoute] MetadataSource source,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        var movies = _metadataService.GetAllMoviesForSource(source).Where(movie => Keeps(movie.Restricted, restricted));
        return await Page(Search(movies, search, fuzzy), movie => _models.Movie(movie, include, language), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get several stored movies of a source at once.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="body">The movies and the extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The movies that are stored, in the order asked for.</returns>
    [HttpPost("Movie/Bulk")]
    public async Task<ActionResult<List<MetadataMovie>>> GetMoviesInBulk(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkFetchBody body,
        CancellationToken cancellationToken = default
    )
    {
        List<MetadataMovie> models = [];
        foreach (var guid in body.IDs.Select(id => FromBody(source, MetadataEntityType.Movie, id)).OfType<MetadataGuid>())
            if (await Get<IMovie>(guid, cancellationToken).ConfigureAwait(false) is { } movie)
                models.Add(_models.Movie(movie, body.Include));

        return models;
    }

    /// <summary>
    /// Get a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The movie.</returns>
    [HttpGet("Movie/{id}")]
    public async Task<ActionResult<MetadataMovie>> GetMovieByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? _models.Movie(movie, include, language)
            : NotFound(MovieNotFound);

    /// <summary>
    /// Purge a stored movie in the background, with the links to it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Movie/{id}")]
    public async Task<ActionResult> DeleteMovie([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Movie, id) is not { } guid)
            return NotFound(MovieNotFound);

        await _purgeService.PurgeEntry(guid, force: true, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Movies | Details

    /// <summary>
    /// Get every title of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The titles, the preferred one first.</returns>
    [HttpGet("Movie/{id}/Titles")]
    public async Task<ActionResult<IReadOnlyList<Title>>> GetMovieTitles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(_models.Titles(movie.ID, language))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get every overview of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Movie/{id}/Overviews")]
    public async Task<ActionResult<IReadOnlyList<Overview>>> GetMovieOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(_models.Overviews(movie.ID, language))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get every image of a stored movie, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The images.</returns>
    [HttpGet("Movie/{id}/Images")]
    public async Task<ActionResult<Images>> GetMovieImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? _models.Images(movie, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, language, includeRemoteUrl)
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the tags of a stored movie, most relevant first.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="kind">Only the tags of this kind: tags, genres or keywords.</param>
    /// <param name="excludeOverviews">Leave the tags' overviews out.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The tags.</returns>
    [HttpGet("Movie/{id}/Tags")]
    public async Task<ActionResult<IReadOnlyList<MetadataTag>>> GetMovieTags(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] TagKind? kind = null,
        [FromQuery] bool excludeOverviews = false,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(_models.Tags(movie.Tags, kind, excludeOverviews))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the studios of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The studios.</returns>
    [HttpGet("Movie/{id}/Studios")]
    public async Task<ActionResult<IReadOnlyList<MetadataStudio>>> GetMovieStudios([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.Studios.Select(_models.Studio).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the cast of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The cast.</returns>
    [HttpGet("Movie/{id}/Cast")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetMovieCast([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.Cast.Select(_models.Cast).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the crew of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The crew.</returns>
    [HttpGet("Movie/{id}/Crew")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetMovieCrew([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.Crew.Select(_models.Crew).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the content ratings of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The content ratings.</returns>
    [HttpGet("Movie/{id}/ContentRatings")]
    public async Task<ActionResult<IReadOnlyList<ContentRating>>> GetMovieContentRatings(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(MetadataModelBuilder.ContentRatings(movie.ContentRatings, language))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the yearly seasons a stored movie came out in.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The yearly seasons.</returns>
    [HttpGet("Movie/{id}/YearlySeasons")]
    public async Task<ActionResult<IReadOnlyList<SeasonWithYear>>> GetMovieYearlySeasons([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.YearlySeasons.ToV3Dto()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the external resources of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The resources.</returns>
    [HttpGet("Movie/{id}/Resources")]
    public async Task<ActionResult<IReadOnlyList<Resource>>> GetMovieResources([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(MetadataModelBuilder.Resources(movie))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get how the source relates a stored movie to its other movies and
    /// series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The relations.</returns>
    [HttpGet("Movie/{id}/Relations")]
    public async Task<ActionResult<IReadOnlyList<MetadataRelation>>> GetMovieRelations([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.RelatedMovies.Cast<IRelatedMetadata>().Concat(movie.RelatedSeries).Select(_models.Relation).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the movies the source suggests beside a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The suggestions.</returns>
    [HttpGet("Movie/{id}/Suggestions")]
    public async Task<ActionResult<IReadOnlyList<MetadataSuggestion>>> GetMovieSuggestions([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.Suggestions.Select(suggestion => _models.Suggestion(suggestion)).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the movies the source suggests a stored movie beside.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The suggestions, each naming the movie suggesting it.</returns>
    [HttpGet("Movie/{id}/SuggestedBy")]
    public async Task<ActionResult<IReadOnlyList<MetadataSuggestion>>> GetMovieSuggestedBy([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? movie.SuggestedBy.Select(suggestion => _models.Suggestion(suggestion, suggestedBy: true)).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the collections a stored movie is in.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The collections.</returns>
    [HttpGet("Movie/{id}/Collection")]
    public async Task<ActionResult<IReadOnlyList<MetadataCollection>>> GetMovieCollections(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? _metadataService.GetCollectionsWith(movie.ID).Select(collection => _models.Collection(collection, Members(collection), include, language)).ToList()
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the links from AniDB to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The links, by AniDB anime and episode.</returns>
    [HttpGet("Movie/{id}/CrossReferences")]
    public async Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> GetMovieCrossReferences([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? Ok(MetadataModelBuilder.CrossReferences(movie.MetadataMovieCrossReferences, _metadataService))
            : NotFound(MovieNotFound);

    #endregion

    #region Movies | Reverse Lookups

    /// <summary>
    /// Get the AniDB anime linked to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The anime.</returns>
    [HttpGet("Movie/{id}/AniDB/Anime")]
    public async Task<ActionResult<List<AnidbAnime>>> GetMovieAnidbAnime([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? AnidbAnime(movie.MetadataMovieCrossReferences)
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the AniDB episodes linked to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The AniDB episodes.</returns>
    [HttpGet("Movie/{id}/AniDB/Episode")]
    public async Task<ActionResult<List<AnidbEpisode>>> GetMovieAnidbEpisodes([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? AnidbEpisodes(movie.MetadataMovieCrossReferences.Select(link => link.AnidbEpisodeID))
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the Shoko series linked to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="randomImages">Pick the series' images at random.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko series the user may see.</returns>
    [HttpGet("Movie/{id}/Shoko/Series")]
    public async Task<ActionResult<List<Series>>> GetMovieShokoSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? ShokoSeries(movie.MetadataMovieCrossReferences.Select(link => link.AnidbAnimeID), randomImages, includeDataFrom)
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the Shoko episodes linked to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko episodes the user may see.</returns>
    [HttpGet("Movie/{id}/Shoko/Episode")]
    public async Task<ActionResult<List<Episode>>> GetMovieShokoEpisodes(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? ShokoEpisodes(movie.MetadataMovieCrossReferences.Select(link => link.AnidbEpisodeID), includeDataFrom)
            : NotFound(MovieNotFound);

    /// <summary>
    /// Get the files of the Shoko episodes linked to a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="include">Include items that are not included by default.</param>
    /// <param name="exclude">Exclude items of certain types.</param>
    /// <param name="include_only">Filter to only include items of certain types.</param>
    /// <param name="releaseProviders">Only files from these release providers; append <c>!</c> to a name to leave its files out.</param>
    /// <param name="sortOrder">Sort order. Put <c>-</c> before a criterion to reverse it.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of files.</returns>
    [HttpGet("Movie/{id}/Shoko/File")]
    public async Task<ActionResult<ListResult<File>>> GetMovieShokoFiles(
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
        => await Get<IMovie>(source, MetadataEntityType.Movie, id, cancellationToken).ConfigureAwait(false) is { } movie
            ? ShokoFiles(movie.MetadataMovieCrossReferences.Select(link => link.AnidbEpisodeID), pageSize, page, include, exclude, include_only, releaseProviders, sortOrder)
            : NotFound(MovieNotFound);

    #endregion

    #region Movies | Actions

    /// <summary>
    /// Refresh a movie of a source, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>200 when it was waited on, 204 when it was queued, or 503 with <c>Retry-After</c> while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Movie/{id}/Action/Refresh")]
    public async Task<ActionResult> RefreshMovie(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataRefreshBody body,
        CancellationToken cancellationToken = default
    )
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Movie, id) is not { } guid)
            return NotFound(MovieNotFound);

        if (_metadataService.GetEntry<IMovie>(guid) is not { } movie)
            return await RefreshMissing(guid, body, cancellationToken).ConfigureAwait(false);

        if (body.SkipIfExists)
            return Ok();

        return await Refresh(movie, body, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Download the images of a stored movie.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="body">How to download them.</param>
    /// <param name="cancellationToken">Cancels a download waited on.</param>
    /// <returns>200 when it was waited on, 204 when it was queued, or 503 with <c>Retry-After</c> while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Movie/{id}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadMovieImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataDownloadImagesBody body,
        CancellationToken cancellationToken = default
    )
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Movie, id) is not { } guid || _metadataService.GetEntry<IMovie>(guid) is not { } movie)
            return NotFound(MovieNotFound);

        return await DownloadImages(movie, body, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #endregion

    #region Collections

    /// <summary>
    /// List the stored collections of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="search">Only the collections whose titles match this.</param>
    /// <param name="fuzzy">Whether to match titles loosely.</param>
    /// <param name="include">The extra details to include: <c>Titles</c>, <c>Overviews</c> and <c>Images</c>.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page.</returns>
    [HttpGet("Collection")]
    public async Task<ActionResult<ListResult<MetadataCollection>>> GetCollections(
        [FromRoute] MetadataSource source,
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
        => await Page(Search(_metadataService.GetAllCollectionsForSource(source), search, fuzzy), collection => _models.Collection(collection, Members(collection), include, language), page, pageSize, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Get a stored collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="include">The extra details to include: <c>Titles</c>, <c>Overviews</c> and <c>Images</c>.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The collection.</returns>
    [HttpGet("Collection/{id}")]
    public async Task<ActionResult<MetadataCollection>> GetCollectionByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? _models.Collection(collection, Members(collection), include, language)
            : NotFound(CollectionNotFound);

    /// <summary>
    /// Get every title of a stored collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The titles, the preferred one first.</returns>
    [HttpGet("Collection/{id}/Titles")]
    public async Task<ActionResult<IReadOnlyList<Title>>> GetCollectionTitles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? Ok(_models.Titles(collection.ID, language))
            : NotFound(CollectionNotFound);

    /// <summary>
    /// Get every overview of a stored collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Collection/{id}/Overviews")]
    public async Task<ActionResult<IReadOnlyList<Overview>>> GetCollectionOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? Ok(_models.Overviews(collection.ID, language))
            : NotFound(CollectionNotFound);

    /// <summary>
    /// Get every image of a stored collection, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The images.</returns>
    [HttpGet("Collection/{id}/Images")]
    public async Task<ActionResult<Images>> GetCollectionImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? _models.Images(collection, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, language, includeRemoteUrl)
            : NotFound(CollectionNotFound);

    /// <summary>
    /// Get the stored movies in a collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The movies.</returns>
    [HttpGet("Collection/{id}/Movie")]
    public async Task<ActionResult<IReadOnlyList<MetadataMovie>>> GetCollectionMovies(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? Members(collection).OfType<IMovie>().Select(movie => _models.Movie(movie, include, language)).ToList()
            : NotFound(CollectionNotFound);

    /// <summary>
    /// Get the stored series in a collection.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the collection.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The series.</returns>
    [HttpGet("Collection/{id}/Series")]
    public async Task<ActionResult<IReadOnlyList<MetadataSeries>>> GetCollectionSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ICollection>(source, MetadataEntityType.Collection, id, cancellationToken).ConfigureAwait(false) is { } collection
            ? Members(collection).OfType<ISeries>().Select(series => _models.Series(series, include, language)).ToList()
            : NotFound(CollectionNotFound);

    #endregion
}
