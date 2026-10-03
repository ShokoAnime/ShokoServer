using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Where every source's cross-references live, so a provider does not have
///   to keep its own. It holds links and nothing else, and does not check who
///   writes them: plugins sharing a source are left to get along.
/// </summary>
/// <remarks>
///   Every write that adds, removes or re-rates a link is reported through
///   <see cref="Services.IMetadataLinkingService.LinksChanged"/>, one event
///   per call made straight to the store.
/// </remarks>
public interface IMetadataCrossReferenceStore
{
    #region Reading

    /// <summary>
    ///   The series an anime is linked to as a whole.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they sit in.</returns>
    IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesLinks(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   The films one episode stands for.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they sit in.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMovieLinks(int anidbEpisodeID, MetadataSource? source = null);

    /// <summary>
    ///   The films every episode of an anime stands for.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they sit in.</returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetMovieLinksForSeries(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   What one episode is linked to.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they sit in.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinks(int anidbEpisodeID, MetadataSource? source = null);

    /// <summary>
    ///   What every episode of an anime is linked to.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in the order they sit in.</returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinksForSeries(int anidbAnimeID, MetadataSource? source = null);

    /// <summary>
    ///   Which AniDB entries claim to be a provider's entry, the other way
    ///   round from the rest. The entry's kind says which level to look at.
    /// </summary>
    /// <param name="entry">The provider's entry: a series, a film or an episode.</param>
    /// <returns>The links, in the order they sit in.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IMetadataCrossReference> GetLinksTo(MetadataGuid entry);

    /// <summary>
    ///   The episode links pointing into a provider's series: those naming it
    ///   as their parent, and those naming an episode the series store holds
    ///   under it. A series linked only at episode level is still in use.
    /// </summary>
    /// <param name="series">The provider's series.</param>
    /// <returns>The links, in the order they sit in; empty for any other kind.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinksInto(MetadataGuid series);

    /// <summary>
    ///   Every series link in the store.
    /// </summary>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, by source, then anime, then the order they sit in.</returns>
    IReadOnlyList<IMetadataSeriesCrossReference> GetAllSeriesLinks(MetadataSource? source = null);

    /// <summary>
    ///   Every film link in the store.
    /// </summary>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>
    ///   The links, by source, then anime, then episode, then the order they
    ///   sit in.
    /// </returns>
    IReadOnlyList<IMetadataMovieCrossReference> GetAllMovieLinks(MetadataSource? source = null);

    /// <summary>
    ///   Every episode link in the store.
    /// </summary>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>
    ///   The links, by source, then anime, then episode, then the order they
    ///   sit in.
    /// </returns>
    IReadOnlyList<IMetadataEpisodeCrossReference> GetAllEpisodeLinks(MetadataSource? source = null);

    /// <summary>
    ///   Every season link, worked out from the episode links the way
    ///   <see cref="IMetadataSeasonCrossReference"/> describes.
    /// </summary>
    /// <remarks>
    ///   Nothing is stored for a season, so this reads every episode link in
    ///   scope and groups them by the season each records. Only seasons some
    ///   link names are found; a link whose season is not known adds none.
    /// </remarks>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>One link per season reached, by anime, then source, then season number.</returns>
    IReadOnlyList<IMetadataSeasonCrossReference> GetAllSeasonLinks(MetadataSource? source = null);

    #endregion

    #region Writing

    /// <summary>
    ///   Writes series links, and takes away the ones named for removal.
    /// </summary>
    /// <remarks>
    ///   A link already there is updated in place; a new one goes after the
    ///   entry's existing links (<see cref="OrderLinks"/> reorders them). Links
    ///   left out are kept unless
    ///   <see cref="MetadataLinkUpdateOptions.ReplaceExisting"/> is set, when an
    ///   entry this write names loses the links it does not.
    /// </remarks>
    /// <param name="links">
    ///   The links to write, which may cover several entries and several
    ///   sources.
    /// </param>
    /// <param name="removals">Optional. The links to take away.</param>
    /// <param name="options">Optional. How to write them.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links this write wrote, removals included.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   <paramref name="links"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   A link names no provider entry, or one on another source, or one that
    ///   is neither a series nor a film claiming the whole anime.
    /// </exception>
    Task<IReadOnlyList<IMetadataSeriesCrossReference>> MergeSeriesLinks(
        IEnumerable<MetadataSeriesLinkData> links,
        IEnumerable<IMetadataSeriesCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Writes film links, and takes away the ones named for removal.
    /// </summary>
    /// <remarks>
    ///   A link already there is updated in place; a new one goes after the
    ///   entry's existing links (<see cref="OrderLinks"/> reorders them). Links
    ///   left out are kept unless
    ///   <see cref="MetadataLinkUpdateOptions.ReplaceExisting"/> is set, when an
    ///   entry this write names loses the links it does not.
    /// </remarks>
    /// <param name="links">
    ///   The links to write, which may cover several entries and several
    ///   sources.
    /// </param>
    /// <param name="removals">Optional. The links to take away.</param>
    /// <param name="options">Optional. How to write them.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links this write wrote, removals included.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   <paramref name="links"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   A link names no provider entry, or one on another source, or one that
    ///   is not a film.
    /// </exception>
    Task<IReadOnlyList<IMetadataMovieCrossReference>> MergeMovieLinks(
        IEnumerable<MetadataMovieLinkData> links,
        IEnumerable<IMetadataMovieCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Writes episode links, and takes away the ones named for removal.
    /// </summary>
    /// <remarks>
    ///   A link already there is updated in place; a new one goes after the
    ///   entry's existing links (<see cref="OrderLinks"/> reorders them). Links
    ///   left out are kept unless
    ///   <see cref="MetadataLinkUpdateOptions.ReplaceExisting"/> is set, when an
    ///   entry this write names loses the links it does not.
    /// </remarks>
    /// <param name="links">
    ///   The links to write, which may cover several entries and several
    ///   sources. A link's series, season and numbers are kept as given; any
    ///   left out are filled from the core's series store when it holds the
    ///   episode, and kept in step by later writes to it.
    /// </param>
    /// <param name="removals">Optional. The links to take away.</param>
    /// <param name="options">Optional. How to write them.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links this write wrote, removals included.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   <paramref name="links"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    ///   A link's provider entry is on another source or is not an episode,
    ///   its parent is on another source or is not a series, or its season is
    ///   on another source or is not a season.
    /// </exception>
    Task<IReadOnlyList<IMetadataEpisodeCrossReference>> MergeEpisodeLinks(
        IEnumerable<MetadataEpisodeLinkData> links,
        IEnumerable<IMetadataEpisodeCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Puts links in the order given, adding and removing nothing. Links of
    ///   the same entry left out keep their order after the ones named here.
    /// </summary>
    /// <param name="links">
    ///   The links in the order wanted, as read back from this store. They may
    ///   cover several entries, and each entry is ordered on its own.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links that moved.</returns>
    /// <exception cref="System.ArgumentNullException">
    ///   <paramref name="links"/> is <c>null</c>.
    /// </exception>
    Task<IReadOnlyList<IMetadataCrossReference>> OrderLinks(
        IEnumerable<IMetadataCrossReference> links,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Removes every link an anime has for a source, its episodes' included.
    /// </summary>
    /// <param name="source">The source to forget.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="entityType">One kind of link, or every kind when left out.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links that were removed.</returns>
    Task<IReadOnlyList<IMetadataCrossReference>> RemoveLinksForSeries(
        MetadataSource source,
        int anidbAnimeID,
        MetadataEntityType? entityType = null,
        CancellationToken cancellationToken = default
    );

    #endregion
}
