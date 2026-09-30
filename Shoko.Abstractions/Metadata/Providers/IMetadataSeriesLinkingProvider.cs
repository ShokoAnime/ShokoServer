using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A series provider users can search and match episodes against, so they
///   can link or correct by hand. The links themselves are the core's.
/// </summary>
/// <remarks>
///   Optional; films are on <see cref="IMetadataMovieLinkingProvider"/>.
///   Linking queues no refresh of your series: the caller asks for one when
///   the series is not stored yet.
/// </remarks>
public interface IMetadataSeriesLinkingProvider : IMetadataSeriesProvider
{
    /// <summary>
    ///   The entity types you accept links for, a subset of series, season and
    ///   episode.
    /// </summary>
    IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; }

    #region Search

    /// <summary>
    ///   Search your source for series a user might link to.
    /// </summary>
    /// <remarks>
    ///   Hand back what the remote said and let the caller pick. Narrow by
    ///   whatever of <paramref name="options"/> you can and ignore the rest.
    /// </remarks>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The page asked for, and how many results there are in total.
    /// </returns>
    Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Look one series up by its ID, the way a search would have offered it.
    /// </summary>
    /// <remarks>
    ///   Optional. Answer from what you already hold when you can, and ask
    ///   your source otherwise; nothing is stored by looking. The default
    ///   answers nothing, which the core reports as not supported.
    /// </remarks>
    /// <param name="seriesID">The series, on your source and of the <c>series</c> kind.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The series, or <see langword="null"/> when your source has none by
    ///   that ID.
    /// </returns>
    /// <exception cref="MetadataProviderUnavailableException">Your source cannot be reached for now.</exception>
    Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default)
        => Task.FromResult<MetadataSeriesSearchResult?>(null);

    #endregion

    #region Episodes

    /// <summary>
    ///   Work out which episodes line up, without committing to it unless
    ///   asked.
    /// </summary>
    /// <remarks>
    ///   The same matching you run for yourself, returned rather than written,
    ///   so a user can see it before agreeing; the caller decides whether it
    ///   is saved.
    /// </remarks>
    /// <param name="anime">The AniDB anime being matched.</param>
    /// <param name="anidbEpisodes">
    ///   The episodes to match, already narrowed to the ones in scope, so you
    ///   neither fetch them nor decide which of them count.
    /// </param>
    /// <param name="providerSeriesID">
    ///   Your series to match into: on your source and of the <c>series</c>
    ///   kind.
    /// </param>
    /// <param name="providerSeasonID">
    ///   One season of it, on your source and of the <c>season</c> kind, when
    ///   you have seasons and only one is wanted. Left out, the whole series
    ///   is matched.
    /// </param>
    /// <param name="existing">
    ///   The links already on record, to honour rather than match again. Left
    ///   out, every episode is matched afresh.
    /// </param>
    /// <param name="considerOtherLinks">
    ///   Whether to leave your episodes that other anime are already linked to
    ///   out of the candidates, or <see langword="null"/> for your own default.
    ///   Ignore it if you have no such notion.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   What your matching came up with, saved or not. A match rather than a
    ///   link, since a preview has not been written and has no place yet.
    ///   Return every episode in scope you decided on, the kept ones
    ///   included: a saved result replaces the links of each episode it
    ///   names.
    /// </returns>
    Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
        IAnidbAnime anime,
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        MetadataGuid providerSeriesID,
        MetadataGuid? providerSeasonID = null,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        bool? considerOtherLinks = null,
        CancellationToken cancellationToken = default
    );

    #endregion
}
