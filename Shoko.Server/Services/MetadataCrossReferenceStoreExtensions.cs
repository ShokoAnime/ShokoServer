using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Tmdb;

namespace Shoko.Server.Services;

/// <summary>
///   Reads over <see cref="IMetadataCrossReferenceStore"/> the core's jobs
///   and actions share, which count the series an episode link points into as
///   linked.
/// </summary>
internal static class MetadataCrossReferenceStoreExtensions
{
    /// <summary>
    ///   Whether anything still links to a provider's series or film, counting
    ///   the episode links pointing into a series.
    /// </summary>
    /// <param name="store">The cross-reference store.</param>
    /// <param name="entry">The series or film.</param>
    /// <returns><see langword="true"/> when something links to it.</returns>
    public static bool IsLinked(this IMetadataCrossReferenceStore store, MetadataGuid entry)
        => store.GetLinksTo(entry).Count > 0 || store.GetEpisodeLinksInto(entry).Count > 0;

    /// <summary>
    ///   Whether anything still links to one of a stored collection's members,
    ///   which keeps the collection from being purged.
    /// </summary>
    /// <remarks>
    ///   A plugin source's members are in the collection store. TMDB keeps
    ///   its collections in tables of its own, which the metadata service
    ///   reads, so a TMDB collection's members are its stored movies.
    /// </remarks>
    /// <param name="store">The cross-reference store.</param>
    /// <param name="collections">The collection store.</param>
    /// <param name="metadataService">Reads a collection the core keeps in tables of its own.</param>
    /// <param name="collection">The collection.</param>
    /// <returns><see langword="true"/> when a member is linked.</returns>
    public static bool IsCollectionInUse(
        this IMetadataCrossReferenceStore store,
        IMetadataCollectionStore collections,
        IMetadataService metadataService,
        MetadataGuid collection
    )
    {
        var members = collection.Source.IsCore
            ? (metadataService.GetCollection(collection) as ITmdbCollection)?.Movies.Select(movie => movie.ID) ?? []
            : collections.GetMembers(collection);
        return members.Any(member => store.IsLinked(member));
    }

    /// <summary>
    ///   The episode links naming no series that are left behind once the
    ///   links to a series go: those of each anime that has no other series
    ///   link on the source, as removing an anime's last series link takes
    ///   them.
    /// </summary>
    /// <param name="store">The cross-reference store.</param>
    /// <param name="series">The series whose links go; any other kind leaves none behind.</param>
    /// <param name="anidbAnimeIDs">The anime whose links to the series go.</param>
    /// <returns>The episode links naming no series that go with them.</returns>
    public static List<IMetadataEpisodeCrossReference> GetUnparentedEpisodeLinksLeftBy(
        this IMetadataCrossReferenceStore store,
        MetadataGuid series,
        IEnumerable<int> anidbAnimeIDs
    )
        => series.EntityType != MetadataEntityType.Series
            ? []
            :
            [
                .. anidbAnimeIDs.Distinct()
                    .Where(animeID => !store.GetSeriesLinks(animeID, series.Source)
                        .Any(link => link.ProviderID != series && link.ProviderID?.EntityType != MetadataEntityType.Movie))
                    .SelectMany(animeID => store.GetEpisodeLinksForSeries(animeID, series.Source))
                    .Where(link => link.ProviderParentID is null),
            ];

    /// <summary>
    ///   The series and films an anime is linked to on a source: those its
    ///   series and film links name, and the series its episode links point
    ///   into.
    /// </summary>
    /// <param name="store">The cross-reference store.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="source">The source.</param>
    /// <returns>The entries, each once.</returns>
    public static List<MetadataGuid> GetLinkedEntries(this IMetadataCrossReferenceStore store, int anidbAnimeID, MetadataSource source)
        =>
        [
            .. store.GetSeriesLinks(anidbAnimeID, source).Select(link => link.ProviderID)
                .Concat(store.GetMovieLinksForSeries(anidbAnimeID, source).Select(link => link.ProviderID))
                .Concat(store.GetEpisodeLinksForSeries(anidbAnimeID, source).Select(link => link.ProviderParentID))
                .OfType<MetadataGuid>()
                .Distinct(),
        ];

    /// <summary>
    ///   Every series and film linked to an anime on a source, with the anime
    ///   linking to each, counting the series episode links point into.
    /// </summary>
    /// <param name="store">The cross-reference store.</param>
    /// <param name="source">The source.</param>
    /// <returns>Each anime and the entry it links to, each pair once.</returns>
    public static List<(int AnidbAnimeID, MetadataGuid Entry)> GetAllLinkedEntries(this IMetadataCrossReferenceStore store, MetadataSource source)
        =>
        [
            .. store.GetAllSeriesLinks(source).Select(link => (link.AnidbAnimeID, Entry: link.ProviderID))
                .Concat(store.GetAllMovieLinks(source).Select(link => (link.AnidbAnimeID, Entry: link.ProviderID)))
                .Concat(store.GetAllEpisodeLinks(source).Select(link => (link.AnidbAnimeID, Entry: link.ProviderParentID)))
                .Where(pair => pair.Entry is not null)
                .Select(pair => (pair.AnidbAnimeID, pair.Entry!))
                .Distinct(),
        ];
}
