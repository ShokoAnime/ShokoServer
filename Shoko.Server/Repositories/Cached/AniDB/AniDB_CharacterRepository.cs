using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_CharacterRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Character, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Character, int>? _characterIDs;

    protected override int SelectKey(AniDB_Character entity)
        => entity.AniDB_CharacterID;

    public override void PopulateIndexes()
    {
        _characterIDs = Cache.CreateIndex(a => a.CharacterID);
    }

    public AniDB_Character? GetByCharacterID(int characterID)
        => ReadLock(() => _characterIDs!.GetOne(characterID));

    public IReadOnlyList<AniDB_Character> GetCharactersForAnime(int animeID)
        => ReadLock(() => RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID).Select(xref => GetByCharacterID(xref.CharacterID)).WhereNotNull().ToList());
}
