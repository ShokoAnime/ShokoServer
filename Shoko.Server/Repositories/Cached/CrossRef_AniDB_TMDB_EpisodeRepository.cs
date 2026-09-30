using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Services;

namespace Shoko.Server.Repositories.Cached;

/// <summary>
/// TMDB's view of the shared episode table, kept so TMDB's own code can go on
/// reading episodes by number.
/// </summary>
/// <param name="repository">The episode-level links.</param>
/// <param name="store">The store, which decides where a link sits.</param>
public class CrossRef_AniDB_TMDB_EpisodeRepository(CrossRef_AniDB_Metadata_EpisodeRepository repository, MetadataCrossReferenceStore store)
{
    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetAll()
        => [.. repository.GetAll().Where(Mine).Select(View)];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByAnidbAnimeID(int animeId)
        => [.. repository.GetByAnidbAnimeID(animeId, MetadataSource.TMDB).Select(View)];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByAnidbEpisodeID(int episodeId)
        => [.. repository.GetByAnidbEpisodeID(episodeId, MetadataSource.TMDB).OrderBy(row => row.Ordering).Select(View)];

    public CrossRef_AniDB_TMDB_Episode? GetByAnidbEpisodeAndTmdbEpisodeIDs(int anidbEpisodeId, int tmdbEpisodeId)
        => GetByAnidbEpisodeID(anidbEpisodeId).FirstOrDefault(xref => xref.TmdbEpisodeID == tmdbEpisodeId);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByTmdbShowID(int showId)
        => [.. repository.GetByProviderParentID(MetadataSource.TMDB, showId.ToString()).Select(View)];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetByTmdbEpisodeID(int episodeId)
        => [.. repository.GetByProviderID(MetadataSource.TMDB, episodeId.ToString()).OrderBy(row => row.Ordering).Select(View)];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetAllByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => [.. GetByTmdbShowID(tmdbId), .. GetByAnidbAnimeID(anidbId)];

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetOnlyByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => [.. GetByAnidbAnimeID(anidbId).Where(xref => xref.TmdbShowID == tmdbId)];

    /// <summary>
    /// Stores the link at the position it carries, moving the episode's other
    /// links out of its way.
    /// </summary>
    /// <param name="xref">The link to store.</param>
    public void Save(CrossRef_AniDB_TMDB_Episode xref)
        => xref.Row = store.AddEpisodeLink(
            MetadataSource.TMDB,
            xref.AnidbAnimeID,
            xref.AnidbEpisodeID,
            ((IMetadataCrossReference)xref).ProviderID,
            ((IMetadataEpisodeCrossReference)xref).ProviderParentID,
            xref.MatchRating,
            xref.Ordering
        );

    /// <summary>
    /// Stores each link at the position it carries, moving the episode's
    /// other links out of its way.
    /// </summary>
    /// <param name="xrefs">The links to store.</param>
    public void Save(IEnumerable<CrossRef_AniDB_TMDB_Episode> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Save(xref);
    }

    /// <summary>
    /// Removes the link, closing the gap it leaves among the episode's links.
    /// </summary>
    /// <param name="xref">The link to remove.</param>
    public void Delete(CrossRef_AniDB_TMDB_Episode xref)
        => store.RemoveEpisodeLink(MetadataSource.TMDB, xref.AnidbAnimeID, xref.AnidbEpisodeID, ((IMetadataCrossReference)xref).ProviderID);

    /// <summary>
    /// Removes each link, closing the gap it leaves among the episode's
    /// links.
    /// </summary>
    /// <param name="xrefs">The links to remove.</param>
    public void Delete(IEnumerable<CrossRef_AniDB_TMDB_Episode> xrefs)
    {
        using var changes = store.BeginChanges();
        foreach (var xref in xrefs)
            Delete(xref);
    }

    private static CrossRef_AniDB_TMDB_Episode View(CrossRef_AniDB_Metadata_Episode row)
        => new(row);

    private static bool Mine(CrossRef_AniDB_Metadata_Episode row)
        => row.Source == MetadataSource.TMDB;
}
