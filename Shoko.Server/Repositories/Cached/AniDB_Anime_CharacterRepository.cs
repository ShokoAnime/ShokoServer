using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_CharacterRepository : BaseCachedRepository<AniDB_Anime_Character, int>
{
    private PocoIndex<int, AniDB_Anime_Character, int> _animeIDs;
    public List<AniDB_Anime_Character> GetByAnimeID(int id)
    {
        return ReadLock(() => _animeIDs.GetMultiple(id));
    }

    public List<AniDB_Anime_Character> GetByCharID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Character>()
                .Where(a => a.CharID == id)
                .ToList();
        });
    }

    public override void PopulateIndexes()
    {
        _animeIDs = new PocoIndex<int, AniDB_Anime_Character, int>(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Anime_Character entity)
    {
        return entity.AniDB_Anime_CharacterID;
    }

    public AniDB_Anime_CharacterRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
