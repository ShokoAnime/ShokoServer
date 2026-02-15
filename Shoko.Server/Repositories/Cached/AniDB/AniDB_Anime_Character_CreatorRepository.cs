using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_Character_CreatorRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Character_Creator, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Character_Creator, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Character_Creator, int>? _characterIDs;

    private PocoIndex<int, AniDB_Anime_Character_Creator, int>? _creatorIDs;

    protected override int SelectKey(AniDB_Anime_Character_Creator entity)
        => entity.AniDB_Anime_Character_CreatorID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _characterIDs = Cache.CreateIndex(a => a.CharacterID);
        _creatorIDs = Cache.CreateIndex(a => a.CreatorID);
    }

    public IReadOnlyList<AniDB_Anime_Character_Creator> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public IReadOnlyList<AniDB_Anime_Character_Creator> GetByCharacterID(int characterID)
        => ReadLock(() => _characterIDs!.GetMultiple(characterID));

    public IReadOnlyList<AniDB_Anime_Character_Creator> GetByCharacterIDAndAnimeID(int characterID, int animeID)
        => GetByCharacterID(characterID).Where(a => a.AnimeID == animeID).ToList();

    public IReadOnlyList<AniDB_Anime_Character_Creator> GetByCreatorID(int creatorID)
        => ReadLock(() => _creatorIDs!.GetMultiple(creatorID));
}
