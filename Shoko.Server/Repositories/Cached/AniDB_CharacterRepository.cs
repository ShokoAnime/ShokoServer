using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_CharacterRepository : BaseCachedRepository<AniDB_Character, int>
{
    private PocoIndex<int, AniDB_Character, int> _charIDs;

    public AniDB_Character GetByCharID(int id)
    {
        return ReadLock(() => _charIDs.GetOne(id));
    }

    public List<AniDB_Character> GetCharactersForAnime(int animeID)
    {
        return ReadLock(() => RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID).Select(a => GetByCharID(a.CharID)).WhereNotNull().ToList());
    }

    public AniDB_CharacterRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }

    public override void PopulateIndexes()
    {
        _charIDs = new PocoIndex<int, AniDB_Character, int>(Cache, a => a.CharID);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(AniDB_Character entity)
    {
        return entity.AniDB_CharacterID;
    }
}
