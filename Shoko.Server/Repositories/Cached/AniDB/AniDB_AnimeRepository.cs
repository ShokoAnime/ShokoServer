using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_AnimeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime, int>(databaseFactory)
{
    private static PocoIndex<int, AniDB_Anime, int>? _animeIDs;

    protected override int SelectKey(AniDB_Anime entity)
        => entity.AniDB_AnimeID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
        foreach (var anime in Cache.GetAll())
            anime.ResetPreferredTitle();
    }

    public AniDB_Anime? GetByAnimeID(int animeID)
        => _animeIDs!.GetOne(animeID);

    public List<AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        => Cache.GetAll()
            .Where(a => a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate)
            .ToList();
}
