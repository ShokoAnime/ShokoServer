using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_AnimeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime, int>? _animeIDs;

    protected override int SelectKey(AniDB_Anime entity)
        => entity.AniDB_AnimeID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
    }

    public AniDB_Anime? GetByAnimeID(int animeID)
        => _animeIDs!.GetOne(animeID);
}
