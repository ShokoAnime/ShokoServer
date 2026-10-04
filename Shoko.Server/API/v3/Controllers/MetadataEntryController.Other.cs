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
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;

using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Controllers;

public partial class MetadataEntryController
{
    #region Creators

    /// <summary>
    /// Get a stored creator.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <param name="include">The extra details to include: <c>Overviews</c>, <c>Images</c> and <c>Resources</c>.</param>
    /// <returns>The creator.</returns>
    [HttpGet("Creator/{id}")]
    public ActionResult<MetadataCreator> GetCreatorByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null
    )
        => Lookup<ICreator>(source, MetadataEntityType.Creator, id) is { } creator
            ? _models.Creator(creator, include)
            : NotFound(CreatorNotFound);

    /// <summary>
    /// Get every overview of a stored creator.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <param name="language">The languages to keep.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Creator/{id}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetCreatorOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
        => Lookup<ICreator>(source, MetadataEntityType.Creator, id) is { } creator
            ? Ok(_models.Overviews(creator.ID, language))
            : NotFound(CreatorNotFound);

    /// <summary>
    /// Get every image of a stored creator, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <returns>The images.</returns>
    [HttpGet("Creator/{id}/Images")]
    public ActionResult<Images> GetCreatorImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable
    )
        => Lookup<ICreator>(source, MetadataEntityType.Creator, id) is { } creator
            ? _models.Images(creator, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, null, includeRemoteUrl)
            : NotFound(CreatorNotFound);

    /// <summary>
    /// Get the external resources of a stored creator.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <returns>The resources.</returns>
    [HttpGet("Creator/{id}/Resources")]
    public ActionResult<IReadOnlyList<Resource>> GetCreatorResources([FromRoute] MetadataSource source, [FromRoute] string id)
        => Lookup<ICreator>(source, MetadataEntityType.Creator, id) is { } creator
            ? Ok(MetadataModelBuilder.Resources(creator))
            : NotFound(CreatorNotFound);

    /// <summary>
    /// Get every credit of a stored creator, cast and crew, across series,
    /// movies and episodes.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page of credits: series first, then movies, then episodes.</returns>
    [HttpGet("Creator/{id}/Roles")]
    public ActionResult<ListResult<MetadataRole>> GetCreatorRoles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (Lookup<ICreator>(source, MetadataEntityType.Creator, id) is not { } creator)
            return NotFound(CreatorNotFound);

        var roles = creator.SeriesCastRoles.Select(_models.Cast)
            .Concat(creator.SeriesCrewRoles.Select(_models.Crew))
            .Concat(creator.MovieCastRoles.Select(_models.Cast))
            .Concat(creator.MovieCrewRoles.Select(_models.Crew))
            .Concat(creator.EpisodeCastRoles.Select(_models.Cast))
            .Concat(creator.EpisodeCrewRoles.Select(_models.Crew));
        return Page(roles, page, pageSize);
    }

    #endregion

    #region Characters

    /// <summary>
    /// Get a stored character.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <param name="include">The extra details to include: <c>Overviews</c>, <c>Images</c> and <c>Resources</c>.</param>
    /// <returns>The character.</returns>
    [HttpGet("Character/{id}")]
    public ActionResult<MetadataCharacter> GetCharacterByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null
    )
        => Lookup<ICharacter>(source, MetadataEntityType.Character, id) is { } character
            ? _models.Character(character, include)
            : NotFound(CharacterNotFound);

    /// <summary>
    /// Get every overview of a stored character.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <param name="language">The languages to keep.</param>
    /// <returns>The overviews, the preferred one first.</returns>
    [HttpGet("Character/{id}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetCharacterOverviews(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
        => Lookup<ICharacter>(source, MetadataEntityType.Character, id) is { } character
            ? Ok(_models.Overviews(character.ID, language))
            : NotFound(CharacterNotFound);

    /// <summary>
    /// Get every image of a stored character, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <returns>The images.</returns>
    [HttpGet("Character/{id}/Images")]
    public ActionResult<Images> GetCharacterImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable
    )
        => Lookup<ICharacter>(source, MetadataEntityType.Character, id) is { } character
            ? _models.Images(character, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, null, includeRemoteUrl)
            : NotFound(CharacterNotFound);

    /// <summary>
    /// Get the external resources of a stored character.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <returns>The resources.</returns>
    [HttpGet("Character/{id}/Resources")]
    public ActionResult<IReadOnlyList<Resource>> GetCharacterResources([FromRoute] MetadataSource source, [FromRoute] string id)
        => Lookup<ICharacter>(source, MetadataEntityType.Character, id) is { } character
            ? Ok(MetadataModelBuilder.Resources(character))
            : NotFound(CharacterNotFound);

    /// <summary>
    /// Get every cast credit of a stored character, across series, movies and
    /// episodes.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page of credits: series first, then movies, then episodes.</returns>
    [HttpGet("Character/{id}/Roles")]
    public ActionResult<ListResult<MetadataRole>> GetCharacterRoles(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (Lookup<ICharacter>(source, MetadataEntityType.Character, id) is not { } character)
            return NotFound(CharacterNotFound);

        var roles = character.SeriesCastRoles.Select(_models.Cast)
            .Concat(character.MovieCastRoles.Select(_models.Cast))
            .Concat(character.EpisodeCastRoles.Select(_models.Cast));
        return Page(roles, page, pageSize);
    }

    #endregion

    #region Tags

    /// <summary>
    /// List the tags of a source.
    /// </summary>
    /// <remarks>
    /// A plugin source's tags are read from the tag store; a core source's are
    /// gathered from its series and movies.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="kind">Only the tags of this kind: tags, genres or keywords.</param>
    /// <param name="restricted">Include, leave out, or only include the tags marking adult content.</param>
    /// <param name="excludeOverviews">Leave the tags' overviews out.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page, by name.</returns>
    [HttpGet("Tag")]
    public ActionResult<ListResult<MetadataTag>> GetTags(
        [FromRoute] MetadataSource source,
        [FromQuery] TagKind? kind = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery] bool excludeOverviews = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => Page(
            AllTags(source, kind)
                .Where(tag => Keeps(tag.IsRestricted, restricted))
                .OrderBy(tag => tag.Name, System.StringComparer.Ordinal)
                .ThenBy(tag => tag.ID)
                .Select(tag => _models.Tag(tag, excludeOverview: excludeOverviews)),
            page,
            pageSize
        );

    /// <summary>
    /// Get a tag.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the tag.</param>
    /// <param name="excludeOverview">Leave the overview out.</param>
    /// <param name="includeCount">Count the series and movies that have the tag.</param>
    /// <returns>The tag.</returns>
    [HttpGet("Tag/{id}")]
    public ActionResult<MetadataTag> GetTagByID(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool excludeOverview = false,
        [FromQuery] bool includeCount = false
    )
        => Lookup<ITag>(source, MetadataEntityType.Tag, id) is { } tag
            ? _models.Tag(tag, excludeOverview: excludeOverview, size: includeCount ? EntriesWithTag(tag).Count : null)
            : NotFound(TagNotFound);

    /// <summary>
    /// Get the stored series that have a tag.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the tag.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="restricted">Include, leave out, or only include the series for adults.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Tag/{id}/Series")]
    public async Task<ActionResult<ListResult<MetadataSeries>>> GetTagSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (Lookup<ITag>(source, MetadataEntityType.Tag, id) is not { } tag)
            return NotFound(TagNotFound);

        var series = EntriesWithTag(tag).OfType<ISeries>().Where(series => Keeps(series.Restricted, restricted));
        return await Page(Search(series, null, false), series => _models.Series(series, include), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get the stored movies that have a tag.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the tag.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="restricted">Include, leave out, or only include the movies for adults.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Tag/{id}/Movie")]
    public async Task<ActionResult<ListResult<MetadataMovie>>> GetTagMovies(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (Lookup<ITag>(source, MetadataEntityType.Tag, id) is not { } tag)
            return NotFound(TagNotFound);

        var movies = EntriesWithTag(tag).OfType<IMovie>().Where(movie => Keeps(movie.Restricted, restricted));
        return await Page(Search(movies, null, false), movie => _models.Movie(movie, include), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Studios

    /// <summary>
    /// Get a studio.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the studio.</param>
    /// <returns>The studio.</returns>
    [HttpGet("Studio/{id}")]
    public ActionResult<MetadataStudio> GetStudioByID([FromRoute] MetadataSource source, [FromRoute] string id)
        => Lookup<IStudio>(source, MetadataEntityType.Studio, id) is { } studio
            ? _models.Studio(studio)
            : NotFound(StudioNotFound);

    /// <summary>
    /// Get every image of a studio, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the studio.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <returns>The images.</returns>
    [HttpGet("Studio/{id}/Images")]
    public ActionResult<Images> GetStudioImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable
    )
        => Lookup<IStudio>(source, MetadataEntityType.Studio, id) is { } studio
            ? _models.Images(studio, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, null, includeRemoteUrl)
            : NotFound(StudioNotFound);

    /// <summary>
    /// Get the stored series a studio worked on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the studio.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Studio/{id}/Series")]
    public async Task<ActionResult<ListResult<MetadataSeries>>> GetStudioSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
        => Lookup<IStudio>(source, MetadataEntityType.Studio, id) is { } studio
            ? await Page(Search(studio.SeriesWorks.DistinctBy(series => series.ID), null, false), series => _models.Series(series, include), page, pageSize, cancellationToken).ConfigureAwait(false)
            : NotFound(StudioNotFound);

    /// <summary>
    /// Get the stored movies a studio worked on.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the studio.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Studio/{id}/Movie")]
    public async Task<ActionResult<ListResult<MetadataMovie>>> GetStudioMovies(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
        => Lookup<IStudio>(source, MetadataEntityType.Studio, id) is { } studio
            ? await Page(Search(studio.MovieWorks.DistinctBy(movie => movie.ID), null, false), movie => _models.Movie(movie, include), page, pageSize, cancellationToken).ConfigureAwait(false)
            : NotFound(StudioNotFound);

    #endregion

    #region Networks

    /// <summary>
    /// Get a network.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the network.</param>
    /// <param name="includeCount">Count the series that aired on the network.</param>
    /// <returns>The network.</returns>
    [HttpGet("Network/{id}")]
    public ActionResult<MetadataNetwork> GetNetworkByID([FromRoute] MetadataSource source, [FromRoute] string id, [FromQuery] bool includeCount = false)
        => Lookup<INetwork>(source, MetadataEntityType.Network, id) is { } network
            ? _models.Network(network, includeCount ? _models.NetworkEntries(network).Count : null)
            : NotFound(NetworkNotFound);

    /// <summary>
    /// Get every image of a network, grouped by type.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the network.</param>
    /// <param name="includeDisabled">Include the disabled images.</param>
    /// <param name="includeUndesired">Include the images not marked for download.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a URL at their source for.</param>
    /// <returns>The images.</returns>
    [HttpGet("Network/{id}/Images")]
    public ActionResult<Images> GetNetworkImages(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable
    )
        => Lookup<INetwork>(source, MetadataEntityType.Network, id) is { } network
            ? _models.Images(network, new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }, null, includeRemoteUrl)
            : NotFound(NetworkNotFound);

    /// <summary>
    /// Get the stored series that aired on a network.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the network.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Network/{id}/Series")]
    public async Task<ActionResult<ListResult<MetadataSeries>>> GetNetworkSeries(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (Lookup<INetwork>(source, MetadataEntityType.Network, id) is not { } network)
            return NotFound(NetworkNotFound);

        var series = _models.NetworkEntries(network).Select(_metadataService.GetEntry).OfType<ISeries>();
        return await Page(Search(series, null, false), series => _models.Series(series, include), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get the stored movies that aired on a network.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the network.</param>
    /// <param name="include">The extra details to include.</param>
    /// <param name="pageSize">The page size; 0 for everything.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="cancellationToken">Stops the waits for running refreshes.</param>
    /// <returns>The page, by title.</returns>
    [HttpGet("Network/{id}/Movie")]
    public async Task<ActionResult<ListResult<MetadataMovie>>> GetNetworkMovies(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<MetadataIncludeDetails>? include = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken cancellationToken = default
    )
    {
        if (Lookup<INetwork>(source, MetadataEntityType.Network, id) is not { } network)
            return NotFound(NetworkNotFound);

        var movies = _models.NetworkEntries(network).Select(_metadataService.GetEntry).OfType<IMovie>();
        return await Page(Search(movies, null, false), movie => _models.Movie(movie, include), page, pageSize, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh a creator of a source from the provider refreshing its
    /// creators one at a time, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the creator.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>
    /// 200 when it was waited on, 204 when it was queued, 400 when no provider
    /// refreshes it or it cannot run now, or 503 while the source is paused.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Creator/{id}/Action/Refresh")]
    public Task<ActionResult> RefreshCreator(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataEntityRefreshBody body,
        CancellationToken cancellationToken = default
    )
        => RefreshEntity(source, MetadataEntityType.Creator, id, body, CreatorNotFound, cancellationToken);

    /// <summary>
    /// Refresh a character of a source from the provider refreshing its
    /// characters one at a time, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the character.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>
    /// 200 when it was waited on, 204 when it was queued, 400 when no provider
    /// refreshes it or it cannot run now, or 503 while the source is paused.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Character/{id}/Action/Refresh")]
    public Task<ActionResult> RefreshCharacter(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataEntityRefreshBody body,
        CancellationToken cancellationToken = default
    )
        => RefreshEntity(source, MetadataEntityType.Character, id, body, CharacterNotFound, cancellationToken);

    /// <summary>
    /// Refresh a studio of a source from the provider refreshing its studios
    /// one at a time, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the studio.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>
    /// 200 when it was waited on, 204 when it was queued, 400 when no provider
    /// refreshes it or it cannot run now, or 503 while the source is paused.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Studio/{id}/Action/Refresh")]
    public Task<ActionResult> RefreshStudio(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataEntityRefreshBody body,
        CancellationToken cancellationToken = default
    )
        => RefreshEntity(source, MetadataEntityType.Studio, id, body, StudioNotFound, cancellationToken);

    /// <summary>
    /// Refresh a network of a source from the provider refreshing its
    /// networks one at a time, or fetch it when it is not stored yet.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the network.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>
    /// 200 when it was waited on, 204 when it was queued, 400 when no provider
    /// refreshes it or it cannot run now, or 503 while the source is paused.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Network/{id}/Action/Refresh")]
    public Task<ActionResult> RefreshNetwork(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataEntityRefreshBody body,
        CancellationToken cancellationToken = default
    )
        => RefreshEntity(source, MetadataEntityType.Network, id, body, NetworkNotFound, cancellationToken);

    /// <summary>
    /// Queues a refresh of a creator, character, studio or network, or runs it
    /// and waits for it.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="id">The ID as the route holds it.</param>
    /// <param name="body">How to refresh it.</param>
    /// <param name="notFound">What to answer when the ID is not valid.</param>
    /// <param name="cancellationToken">Cancels a refresh waited on.</param>
    /// <returns>
    /// 200 when it ran, 204 when it was queued, 400 when no provider refreshes
    /// it or it cannot run now, or 503 while the source is paused.
    /// </returns>
    private async Task<ActionResult> RefreshEntity(
        MetadataSource source,
        MetadataEntityType kind,
        string id,
        MetadataEntityRefreshBody body,
        string notFound,
        CancellationToken cancellationToken
    )
    {
        if (ToGuid(source, kind, id) is not { } guid)
            return NotFound(notFound);

        var paused = await QueueWhenPaused(
            source,
            () => _refreshService.RefreshEntry(guid, body.Force, prioritize: true, cancellationToken: cancellationToken),
            "A refresh",
            body.Immediate
        ).ConfigureAwait(false);
        if (paused is not null)
            return paused;

        if (!await _refreshService.RefreshEntry(guid, body.Force, immediate: body.Immediate, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            ModelState.AddModelError("source", $"No enabled metadata provider refreshes the {kind.Value} entries of {source.Name}, or it cannot run now.");
            return ValidationProblem(ModelState);
        }

        return body.Immediate ? Ok() : NoContent();
    }

    #endregion

    #region Helpers | Plain Lookup

    /// <summary>
    /// Look up a stored person, tag, studio or network, a stub included.
    /// </summary>
    /// <typeparam name="TMetadata">The entry's type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry.</param>
    /// <param name="id">The ID as the route holds it.</param>
    /// <returns>The entry, or <c>null</c> when it is not stored.</returns>
    private TMetadata? Lookup<TMetadata>(MetadataSource source, MetadataEntityType kind, string id) where TMetadata : class, IMetadata
        => ToGuid(source, kind, id) is { } guid ? _metadataService.GetEntry<TMetadata>(guid) : null;

    #endregion
}
