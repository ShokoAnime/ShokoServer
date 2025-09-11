﻿#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_CharacterRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Character, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Character, int>? _animeIDs;

    private PocoIndex<int, AniDB_Anime_Character, int>? _characterIDs;

    protected override int SelectKey(AniDB_Anime_Character entity)
        => entity.AniDB_Anime_CharacterID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
        _characterIDs = Cache.CreateIndex(a => a.CharacterID);
    }

    public IReadOnlyList<AniDB_Anime_Character> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public IReadOnlyList<AniDB_Anime_Character> GetByCharacterID(int characterID)
        => ReadLock(() => _characterIDs!.GetMultiple(characterID));

    public AniDB_Anime_Character? GetByAnimeIDAndCharacterID(int animeID, int characterID)
        => GetByCharacterID(characterID).FirstOrDefault(a => a.AnimeID == animeID);
}
