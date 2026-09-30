using System;
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
using Shoko.Server.API.v3.Models.Ordering;
using Shoko.Server.API.v3.Models.Shoko;

using File = Shoko.Server.API.v3.Models.Shoko.File;
using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Controllers;

public partial class MetadataEntryController
{
    #region Series

    #region Series | Basics

    /// <summary>
    /// List the stored series of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="search">Only the series whose titles match this.</param>
    /// <param name="fuzzy">Whether to match titles loosely.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="restricted">Include, leave out, or only include the series for adults.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page.</returns>
    [HttpGet("Series")]
    public async Task<ActionResult<ListResult<MetadataSeries>>> GetSeries(
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
        var series = _metadataService.GetAllSeriesForSource(source).Where(series => Keeps(series.Restricted, restricted));
        return await Page(Search(series, search, fuzzy), series => _models.Series(series, include, language), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get several stored series of a source at once.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="body">The series and the extra details to include.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The series that are stored, in the order asked for.</returns>
    [HttpPost("Series/Bulk")]
    public async Task<ActionResult<List<MetadataSeries>>> GetSeriesInBulk(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkFetchBody body,
        CancellationToken cancellationToken = default
    )
    {
        List<MetadataSeries> models = [];
        foreach (var guid in body.IDs.Select(id => FromBody(source, MetadataEntityType.Series, id)).OfType<MetadataGuid>())
            if (await Get<ISeries>(guid, cancellationToken).ConfigureAwait(false) is { } series)
                models.Add(_models.Series(series, body.Include));

        return models;
    }

    /// <summary>
    /// Get a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The series.</returns>
    [HttpGet("Series/{id}")]
    public async Task<ActionResult<MetadataSeries>> GetSeriesByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? _models.Series(series, include, language)
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Purge a stored series in the background, with the links to it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    [Authorize("admin")]
    [HttpDelete("Series/{id}")]
    public async Task<ActionResult> DeleteSeries([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Series, id) is not { } guid)
            return NotFound(SeriesNotFound);

        await _purgeService.PurgeEntry(guid, force: true, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Series | Text and Images

    /// <summary>
    /// Get every title of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The titles, the preferred one first.</returns>
    [HttpGet("Series/{id}/Titles")]
    public async Task<ActionResult<IReadOnlyList<Title>>> GetSeriesTitles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(_models.Titles(series.ID, language))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get every overview of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Series/{id}/Overviews")]
    public async Task<ActionResult<IReadOnlyList<Overview>>> GetSeriesOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(_models.Overviews(series.ID, language))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get every image of a stored series, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The images.</returns>
    [HttpGet("Series/{id}/Images")]
    public async Task<ActionResult<Images>> GetSeriesImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? _models.Images(series, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, language, includeRemoteUrl)
            : NotFound(SeriesNotFound);

    #endregion

    #region Series | Classification

    /// <summary>
    /// Get the tags of a stored series, most relevant first.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="kind">Only the tags of this kind: tags, genres or keywords.</param>
    /// <param name="excludeOverviews">Leave the tags' overviews out.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The tags.</returns>
    [HttpGet("Series/{id}/Tags")]
    public async Task<ActionResult<IReadOnlyList<MetadataTag>>> GetSeriesTags(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] TagKind? kind = null,
        [FromQuery] bool excludeOverviews = false,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(_models.Tags(series.Tags, kind, excludeOverviews))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the studios of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The studios.</returns>
    [HttpGet("Series/{id}/Studios")]
    public async Task<ActionResult<IReadOnlyList<MetadataStudio>>> GetSeriesStudios([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Studios.Select(_models.Studio).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the networks a stored series aired on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The networks.</returns>
    [HttpGet("Series/{id}/Networks")]
    public async Task<ActionResult<IReadOnlyList<MetadataNetwork>>> GetSeriesNetworks([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Networks.Select(network => _models.Network(network)).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the content ratings of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="language">The languages to keep.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The content ratings.</returns>
    [HttpGet("Series/{id}/ContentRatings")]
    public async Task<ActionResult<IReadOnlyList<ContentRating>>> GetSeriesContentRatings(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(MetadataModelBuilder.ContentRatings(series.ContentRatings, language))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the yearly seasons a stored series aired in.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The yearly seasons.</returns>
    [HttpGet("Series/{id}/YearlySeasons")]
    public async Task<ActionResult<IReadOnlyList<SeasonWithYear>>> GetSeriesYearlySeasons([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.YearlySeasons.ToV3Dto()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the days of the week a stored series aired episodes on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The days' names, in alphabetical order.</returns>
    [HttpGet("Series/{id}/DaysOfWeek")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSeriesDaysOfWeek([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(DaysOfWeek(series.Episodes))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the external resources of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The resources.</returns>
    [HttpGet("Series/{id}/Resources")]
    public async Task<ActionResult<IReadOnlyList<Resource>>> GetSeriesResources([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(MetadataModelBuilder.Resources(series))
            : NotFound(SeriesNotFound);

    #endregion

    #region Series | Credits

    /// <summary>
    /// Get the cast of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The cast.</returns>
    [HttpGet("Series/{id}/Cast")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetSeriesCast([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Cast.Select(_models.Cast).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the crew of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The crew.</returns>
    [HttpGet("Series/{id}/Crew")]
    public async Task<ActionResult<IReadOnlyList<MetadataRole>>> GetSeriesCrew([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Crew.Select(_models.Crew).ToList()
            : NotFound(SeriesNotFound);

    #endregion

    #region Series | Related Entries

    /// <summary>
    /// Get how the source relates a stored series to its other series and
    /// movies.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The relations.</returns>
    [HttpGet("Series/{id}/Relations")]
    public async Task<ActionResult<IReadOnlyList<MetadataRelation>>> GetSeriesRelations([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.RelatedSeries.Cast<IRelatedMetadata>().Concat(series.RelatedMovies).Select(_models.Relation).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the series the source suggests beside a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The suggestions.</returns>
    [HttpGet("Series/{id}/Suggestions")]
    public async Task<ActionResult<IReadOnlyList<MetadataSuggestion>>> GetSeriesSuggestions([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Suggestions.Select(suggestion => _models.Suggestion(suggestion)).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the series the source suggests a stored series beside.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The suggestions, each naming the series suggesting it.</returns>
    [HttpGet("Series/{id}/SuggestedBy")]
    public async Task<ActionResult<IReadOnlyList<MetadataSuggestion>>> GetSeriesSuggestedBy([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.SuggestedBy.Select(suggestion => _models.Suggestion(suggestion, suggestedBy: true)).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get every ordering of a stored series: the source's own, other
    /// sources' and the users'.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="includeGroups">Include each ordering's groups and their episodes.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The orderings.</returns>
    [HttpGet("Series/{id}/Orderings")]
    public async Task<ActionResult<IReadOnlyList<SeriesOrdering>>> GetSeriesOrderings(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeGroups = false,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? _orderingService.GetOrderings(series).Select(ordering => new SeriesOrdering(ordering, includeGroups)).ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the seasons of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The seasons, in order.</returns>
    [HttpGet("Series/{id}/Season")]
    public async Task<ActionResult<IReadOnlyList<MetadataSeason>>> GetSeriesSeasons(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? series.Seasons
                .OrderBy(season => season.SeasonNumber is 0 ? int.MaxValue : season.SeasonNumber)
                .Select(season => _models.Season(season, include, language))
                .ToList()
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the episodes of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="search">
    /// Filters on the episode number. A bare number matches anywhere in it;
    /// with an <c>E</c> or <c>#</c> in front it matches that episode only.
    /// </param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="language">The languages of the titles, overviews and images to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page, by season then number, specials last.</returns>
    [HttpGet("Series/{id}/Episode")]
    public async Task<ActionResult<ListResult<MetadataEpisode>>> GetSeriesEpisodes(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] string? search = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is not { } series)
            return NotFound(SeriesNotFound);

        if (SearchEpisodes(series.Episodes, search) is not { } episodes)
            return new ListResult<MetadataEpisode>();

        return Page(InOrder(episodes).Select(episode => _models.Episode(episode, include, language)), page, pageSize);
    }

    #endregion

    #region Series | Links

    /// <summary>
    /// Get the links from AniDB anime to a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The links, by AniDB anime.</returns>
    [HttpGet("Series/{id}/CrossReferences")]
    public async Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> GetSeriesCrossReferences([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Ok(MetadataModelBuilder.CrossReferences(series.MetadataSeriesCrossReferences))
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the links from AniDB episodes into a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of links, by AniDB anime and episode.</returns>
    [HttpGet("Series/{id}/Episode/CrossReferences")]
    public async Task<ActionResult<ListResult<MetadataCrossReference>>> GetSeriesEpisodeCrossReferences(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? Page(MetadataModelBuilder.CrossReferences(series.MetadataEpisodeCrossReferences), page, pageSize)
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the links from AniDB episodes into a stored series, grouped: an
    /// AniDB episode linked to several episodes is one group, and so are the
    /// AniDB episodes sharing one episode.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of groups, in AniDB's order.</returns>
    [HttpGet("Series/{id}/Episode/CrossReferences/EpisodeGroups")]
    public async Task<ActionResult<ListResult<List<MetadataCrossReference>>>> GetSeriesEpisodeCrossReferenceGroups(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is not { } series)
            return NotFound(SeriesNotFound);

        var groups = GroupEpisodeLinks(series.MetadataEpisodeCrossReferences)
            .Select(group => group.Select((link, index) => Reindex(MetadataModelBuilder.CrossReference(link), index)).ToList());
        return Page(groups, page, pageSize);
    }

    /// <summary>
    /// A link placed at its position in a group.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="index">Its position.</param>
    /// <returns>The link with its index set.</returns>
    private static MetadataCrossReference Reindex(MetadataCrossReference link, int index)
        => new()
        {
            Source = link.Source,
            EntityType = link.EntityType,
            AnidbAnimeID = link.AnidbAnimeID,
            AnidbEpisodeID = link.AnidbEpisodeID,
            ID = link.ID,
            ParentID = link.ParentID,
            SeasonID = link.SeasonID,
            SeasonNumber = link.SeasonNumber,
            EpisodeNumber = link.EpisodeNumber,
            Index = index,
            MatchRating = link.MatchRating,
            WrittenBy = link.WrittenBy,
        };

    #endregion

    #region Series | Reverse Lookups

    /// <summary>
    /// Get the AniDB anime linked to a stored series, by the series or by
    /// its episodes.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The anime.</returns>
    [HttpGet("Series/{id}/AniDB/Anime")]
    public async Task<ActionResult<List<AnidbAnime>>> GetSeriesAnidbAnime([FromRoute] MetadataSource source, [FromRoute] string id, CancellationToken cancellationToken = default)
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? AnidbAnime([.. series.MetadataSeriesCrossReferences, .. series.MetadataEpisodeCrossReferences])
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the Shoko series linked to a stored series, by the series or by
    /// its episodes.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="randomImages">Pick the series' images at random.</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The Shoko series the user may see.</returns>
    [HttpGet("Series/{id}/Shoko/Series")]
    public async Task<ActionResult<List<Series>>> GetSeriesShokoSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        CancellationToken cancellationToken = default
    )
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? ShokoSeries([.. series.MetadataSeriesCrossReferences.Select(link => link.AnidbAnimeID), .. series.MetadataEpisodeCrossReferences.Select(link => link.AnidbAnimeID)], randomImages, includeDataFrom)
            : NotFound(SeriesNotFound);

    /// <summary>
    /// Get the files of the Shoko episodes linked into a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="include">Include items that are not included by default.</param>
    /// <param name="exclude">Exclude items of certain types.</param>
    /// <param name="include_only">Filter to only include items of certain types.</param>
    /// <param name="releaseProviders">Only files from these release providers; append <c>!</c> to a name to leave its files out.</param>
    /// <param name="sortOrder">Sort order. Put <c>-</c> before a criterion to reverse it.</param>
    /// <param name="cancellationToken">Stops the wait for a running refresh.</param>
    /// <returns>The page of files.</returns>
    [HttpGet("Series/{id}/Shoko/File")]
    public async Task<ActionResult<ListResult<File>>> GetSeriesShokoFiles(
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
        => await Get<ISeries>(source, MetadataEntityType.Series, id, cancellationToken).ConfigureAwait(false) is { } series
            ? ShokoFiles(series.MetadataEpisodeCrossReferences.Select(link => link.AnidbEpisodeID), pageSize, page, include, exclude, include_only, releaseProviders, sortOrder)
            : NotFound(SeriesNotFound);

    #endregion

    #region Series | Actions

    /// <summary>
    /// Refresh a series of a source, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>200 when it was waited on, 204 when it was queued, or 503 with <c>Retry-After</c> while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Series/{id}/Action/Refresh")]
    public async Task<ActionResult> RefreshSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataRefreshBody body,
        CancellationToken cancellationToken = default
    )
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Series, id) is not { } guid)
            return NotFound(SeriesNotFound);

        if (_metadataService.GetEntry<ISeries>(guid) is not { } series)
            return await RefreshMissing(guid, body, cancellationToken).ConfigureAwait(false);

        if (body.SkipIfExists)
            return Ok();

        return await Refresh(series, body, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Download the images of a stored series.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="body">How to download them.</param>
    /// <param name="cancellationToken">Cancels a download waited on.</param>
    /// <returns>200 when it was waited on, 204 when it was queued, or 503 with <c>Retry-After</c> while the source is paused.</returns>
    [Authorize("admin")]
    [HttpPost("Series/{id}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadSeriesImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataDownloadImagesBody body,
        CancellationToken cancellationToken = default
    )
    {
        if (RefuseCoreRefresh(source) is { } refused)
            return refused;

        if (ToGuid(source, MetadataEntityType.Series, id) is not { } guid || _metadataService.GetEntry<ISeries>(guid) is not { } series)
            return NotFound(SeriesNotFound);

        return await DownloadImages(series, body, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #endregion
}
