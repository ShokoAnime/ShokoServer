using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Repositories.Cached;

/// <summary>
/// TMDB's view of the shared film table, kept so TMDB's own code can go on
/// reading movies by number.
/// </summary>
/// <param name="repository">The film-level links.</param>
/// <param name="store">The store, which decides where a link sits.</param>
public class CrossRef_AniDB_TMDB_MovieRepository(CrossRef_AniDB_Metadata_MovieRepository repository, MetadataCrossReferenceStore store)
{
    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetAll()
        => [.. repository.GetAll().Where(Mine).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbAnimeID(int animeId)
        => [.. repository.GetByAnidbAnimeID(animeId, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbEpisodeID(int episodeId)
        => [.. repository.GetByAnidbEpisodeID(episodeId, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    public CrossRef_AniDB_TMDB_Movie? GetByAnidbEpisodeAndTmdbMovieIDs(int episodeId, int movieId)
        => GetByAnidbEpisodeID(episodeId).FirstOrDefault(xref => xref.TmdbMovieID == movieId);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByTmdbMovieID(int movieId)
        => [.. repository.GetByProviderID(MetadataSource.TMDB, movieId.ToString()).Select(row => new CrossRef_AniDB_TMDB_Movie(row))];

    /// <summary>
    /// Stores the link, letting the store place it among the episode's links.
    /// </summary>
    /// <param name="xref">The link to store.</param>
    public void Save(CrossRef_AniDB_TMDB_Movie xref)
        => xref.Row = store.AddMovieLink(
            MetadataSource.TMDB,
            xref.AnidbAnimeID,
            xref.AnidbEpisodeID,
            ((IMetadataCrossReference)xref).ProviderID,
            xref.MatchRating
        );

    /// <summary>
    /// Stores each link, letting the store place it among the episode's
    /// links.
    /// </summary>
    /// <param name="xrefs">The links to store.</param>
    public void Save(IEnumerable<CrossRef_AniDB_TMDB_Movie> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Save(xref);
    }

    /// <summary>
    /// Removes the link, closing the gap it leaves among the episode's links.
    /// </summary>
    /// <param name="xref">The link to remove.</param>
    public void Delete(CrossRef_AniDB_TMDB_Movie xref)
        => store.RemoveMovieLink(MetadataSource.TMDB, xref.AnidbAnimeID, xref.AnidbEpisodeID, ((IMetadataCrossReference)xref).ProviderID);

    /// <summary>
    /// Removes each link, closing the gap it leaves among the episode's
    /// links.
    /// </summary>
    /// <param name="xrefs">The links to remove.</param>
    public void Delete(IEnumerable<CrossRef_AniDB_TMDB_Movie> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Delete(xref);
    }

    private static bool Mine(CrossRef_AniDB_Metadata_Movie row)
        => row.Source == MetadataSource.TMDB;
}
