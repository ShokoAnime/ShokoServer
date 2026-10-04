using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;

namespace Shoko.Server.API.v3.Controllers;

public partial class MetadataEntryController
{
    #region Match Ratings

    /// <summary>
    /// Set how far the links from AniDB anime to a series are trusted,
    /// verifying them unless told otherwise. Nothing is removed or matched.
    /// </summary>
    /// <remarks>
    /// The series does not need to be stored. Only the rating changes: each
    /// link keeps its place and the episode links under it stay as they are.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the series.</param>
    /// <param name="body">The rating, and which links; every link verified when left out.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The links changed, as stored now.</returns>
    [Authorize("admin")]
    [HttpPatch("Series/{id}/CrossReferences")]
    public Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> SetSeriesMatchRating(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataSetMatchRatingBody? body = null,
        CancellationToken cancellationToken = default
    )
        => SetMatchRating(source, MetadataEntityType.Series, id, body, cancellationToken);

    /// <summary>
    /// Set how far the links from AniDB to a movie are trusted, verifying them
    /// unless told otherwise. Nothing is removed or matched.
    /// </summary>
    /// <remarks>
    /// The movie does not need to be stored. Covers the movie's links from
    /// AniDB episodes and its claims on whole anime.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the movie.</param>
    /// <param name="body">The rating, and which links; every link verified when left out.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The links changed, as stored now.</returns>
    [Authorize("admin")]
    [HttpPatch("Movie/{id}/CrossReferences")]
    public Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> SetMovieMatchRating(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataSetMatchRatingBody? body = null,
        CancellationToken cancellationToken = default
    )
        => SetMatchRating(source, MetadataEntityType.Movie, id, body, cancellationToken);

    /// <summary>
    /// Set how far the links from AniDB episodes to an episode are trusted,
    /// verifying them unless told otherwise. Nothing is removed or matched.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="id">The source's ID for the episode.</param>
    /// <param name="body">The rating, and which links; every link verified when left out.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The links changed, as stored now.</returns>
    [Authorize("admin")]
    [HttpPatch("Episode/{id}/CrossReferences")]
    public Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> SetEpisodeMatchRating(
        [FromRoute] MetadataSource source,
        [FromRoute] string id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MetadataSetMatchRatingBody? body = null,
        CancellationToken cancellationToken = default
    )
        => SetMatchRating(source, MetadataEntityType.Episode, id, body, cancellationToken);

    /// <summary>
    /// Set how far several links to a source are trusted at once, verifying
    /// them unless told otherwise. Nothing is removed or matched.
    /// </summary>
    /// <remarks>
    /// Each link is named by the fields the cross-reference routes send, so
    /// what one of them returned can be sent back as it is. A link that is
    /// not stored refuses the whole request, and nothing changes.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="body">The rating and the links.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The links changed, as stored now.</returns>
    [Authorize("admin")]
    [HttpPatch("CrossReferences")]
    public async Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> SetMatchRatings(
        [FromRoute] MetadataSource source,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] MetadataBulkSetMatchRatingBody body,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!Enum.IsDefined(body.MatchRating))
            return ValidationProblem($"'{body.MatchRating}' is not a match rating.", nameof(body.MatchRating));

        var links = new List<IMetadataCrossReference>();
        for (var index = 0; index < body.CrossReferences.Count; index++)
        {
            var key = body.CrossReferences[index];
            var found = StoredLinks(source, key);
            if (found.Count is 0)
                ModelState.AddModelError($"{nameof(body.CrossReferences)}[{index}]", $"No {key.EntityType.Value} link from AniDB matches '{key.ID}'.");
            else
                links.AddRange(found);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var updated = await _linkingService.SetMatchRating(links, body.MatchRating, cancellationToken).ConfigureAwait(false);
        return Ok(MetadataModelBuilder.CrossReferences(updated, _metadataService));
    }

    /// <summary>
    /// Sets the rating of the links to one entry, narrowed as the body asks.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">The kind of entry the route names.</param>
    /// <param name="id">The ID as the route holds it.</param>
    /// <param name="body">The rating, and which links, or <c>null</c> to verify every one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The links changed, or <c>404 Not Found</c> when none matched.</returns>
    private async Task<ActionResult<IReadOnlyList<MetadataCrossReference>>> SetMatchRating(
        MetadataSource source,
        MetadataEntityType kind,
        string id,
        MetadataSetMatchRatingBody? body,
        CancellationToken cancellationToken
    )
    {
        body ??= new();
        if (!Enum.IsDefined(body.MatchRating))
            return ValidationProblem($"'{body.MatchRating}' is not a match rating.", nameof(body.MatchRating));

        if (ToGuid(source, kind, id) is not { } entry)
            return NotFound(CrossReferencesNotFound);

        var links = _crossReferences.GetLinksTo(entry)
            .Where(link => body.AnidbAnimeID is not { } animeID || link.AnidbAnimeID == animeID)
            .Where(link => body.AnidbEpisodeID is not { } episodeID || AnidbEpisodeOf(link) == episodeID)
            .ToList();
        if (links.Count is 0)
            return NotFound(CrossReferencesNotFound);

        var updated = await _linkingService.SetMatchRating(links, body.MatchRating, cancellationToken).ConfigureAwait(false);
        return Ok(MetadataModelBuilder.CrossReferences(updated, _metadataService));
    }

    /// <summary>
    /// The stored links a body names: those at its level, from its AniDB
    /// entry, to the entry it names or to nothing.
    /// </summary>
    /// <remarks>
    /// A bare ID at the series level names both a series and a film claiming
    /// the whole anime, where both are linked.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="key">The link as the body names it.</param>
    /// <returns>The links, none when nothing matches or the level is not stored.</returns>
    private List<IMetadataCrossReference> StoredLinks(MetadataSource source, MetadataCrossReferenceKeyBody key)
    {
        IEnumerable<IMetadataCrossReference> candidates = key.EntityType switch
        {
            _ when key.EntityType == MetadataEntityType.Series => _crossReferences.GetSeriesLinks(key.AnidbAnimeID, source),
            _ when key.EntityType == MetadataEntityType.Movie && key.AnidbEpisodeID is { } episodeID => _crossReferences.GetMovieLinks(episodeID, source),
            _ when key.EntityType == MetadataEntityType.Episode && key.AnidbEpisodeID is { } episodeID => _crossReferences.GetEpisodeLinks(episodeID, source),
            _ => [],
        };

        return [.. candidates.Where(link => link.AnidbAnimeID == key.AnidbAnimeID && Names(link, key.ID))];
    }

    /// <summary>
    /// Checks whether a link names the entry a body names, by the source's
    /// own ID or a full identifier, or names nothing when the body names none.
    /// </summary>
    /// <param name="link">The stored link.</param>
    /// <param name="id">The ID the body gives.</param>
    /// <returns><c>true</c> when they name the same entry.</returns>
    private static bool Names(IMetadataCrossReference link, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return link.ProviderID is null;

        if (link.ProviderID is not { } providerID)
            return false;

        return id.Contains("://", StringComparison.Ordinal)
            ? MetadataGuid.TryParse(id, out var guid) && guid == providerID
            : string.Equals(providerID.ID, id, StringComparison.Ordinal);
    }

    /// <summary>
    /// The AniDB episode a link is made from, where its level has one.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>The AniDB episode ID, or <c>null</c> at the series level.</returns>
    private static int? AnidbEpisodeOf(IMetadataCrossReference link)
        => link switch
        {
            IMetadataMovieCrossReference movie => movie.AnidbEpisodeID,
            IMetadataEpisodeCrossReference episode => episode.AnidbEpisodeID,
            _ => null,
        };

    #endregion
}
