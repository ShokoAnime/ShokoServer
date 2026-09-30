using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Repositories.Cached;

/// <summary>
/// TMDB's view of the shared cross-reference table, kept so TMDB's own code
/// can go on reading shows by number.
/// </summary>
public class CrossRef_AniDB_TMDB_ShowRepository(CrossRef_AniDB_Metadata_SeriesRepository repository, MetadataCrossReferenceStore store)
{

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetAll()
        => [.. repository.GetAll().Where(Mine).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByAnidbAnimeID(int animeId)
        => [.. repository.GetByAnidbAnimeID(animeId, MetadataSource.TMDB).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetByTmdbShowID(int showId)
        => [.. repository.GetByProviderID(MetadataSource.TMDB, showId.ToString()).Select(row => new CrossRef_AniDB_TMDB_Show(row))];

    public CrossRef_AniDB_TMDB_Show? GetByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => GetByAnidbAnimeID(anidbId).FirstOrDefault(xref => xref.TmdbShowID == tmdbId);

    /// <summary>
    /// Stores the link, letting the store place it among the anime's links.
    /// </summary>
    /// <param name="xref">The link to store.</param>
    public void Save(CrossRef_AniDB_TMDB_Show xref)
        => xref.Row = store.AddSeriesLink(MetadataSource.TMDB, xref.AnidbAnimeID, ((IMetadataCrossReference)xref).ProviderID, xref.MatchRating);

    /// <summary>
    /// Stores each link, letting the store place it among the anime's links.
    /// </summary>
    /// <param name="xrefs">The links to store.</param>
    public void Save(IEnumerable<CrossRef_AniDB_TMDB_Show> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Save(xref);
    }

    /// <summary>
    /// Removes the link, closing the gap it leaves among the anime's links.
    /// </summary>
    /// <param name="xref">The link to remove.</param>
    public void Delete(CrossRef_AniDB_TMDB_Show xref)
        => store.RemoveSeriesLink(MetadataSource.TMDB, xref.AnidbAnimeID, ((IMetadataCrossReference)xref).ProviderID);

    /// <summary>
    /// Removes each link, closing the gap it leaves among the anime's
    /// links.
    /// </summary>
    /// <param name="xrefs">The links to remove.</param>
    public void Delete(IEnumerable<CrossRef_AniDB_TMDB_Show> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Delete(xref);
    }

    private static bool Mine(CrossRef_AniDB_Metadata_Series row)
        => row.Source == MetadataSource.TMDB;
}
