using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_ResourceRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Resource, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Resource, int>? _animeIDs;

    private PocoIndex<int, AniDB_Resource, int>? _episodeIDs;

    protected override int SelectKey(AniDB_Resource entity)
        => entity.AniDB_ResourceID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _episodeIDs = Cache.CreateIndex(a => a.EpisodeID is { } episodeID ? [episodeID] : (IEnumerable<int>)[]);
    }

    /// <summary>
    ///   The resources of an anime itself, in AniDB's order.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The anime's own resources.</returns>
    public List<AniDB_Resource> GetByAnimeID(int animeID)
        => _animeIDs!.GetMultiple(animeID)
            .Where(a => a.EpisodeID is null)
            .OrderBy(a => a.Ordering)
            .ToList();

    /// <summary>
    ///   The resources of an anime and of all its episodes.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>Every resource row kept for the anime.</returns>
    public List<AniDB_Resource> GetAllByAnimeID(int animeID)
        => _animeIDs!.GetMultiple(animeID);

    /// <summary>
    ///   The resources of an episode, in AniDB's order.
    /// </summary>
    /// <param name="episodeID">The AniDB episode ID.</param>
    /// <returns>The episode's resources.</returns>
    public List<AniDB_Resource> GetByEpisodeID(int episodeID)
        => _episodeIDs!.GetMultiple(episodeID)
            .OrderBy(a => a.Ordering)
            .ToList();
}
