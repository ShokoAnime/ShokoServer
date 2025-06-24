#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_AnimeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AniDB_Anime, int>(databaseFactory)
{
    private static PocoIndex<int, SVR_AniDB_Anime, int>? _animeIDs;

    protected override int SelectKey(SVR_AniDB_Anime entity)
        => entity.AniDB_AnimeID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
        foreach (var anime in Cache.Values.ToList())
            anime.ResetPreferredTitle();
    }

    public SVR_AniDB_Anime? GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetOne(animeID));

    public List<SVR_AniDB_Anime> GetForDate(DateTime startDate, DateTime endDate)
        => ReadLock(() =>
            Cache.Values
                .Where(a => a.AirDate.HasValue && a.AirDate.Value >= startDate && a.AirDate.Value <= endDate)
                .ToList()
        );

    public List<SVR_AniDB_Anime> SearchByName(string name)
        => ReadLock(() =>
            Cache.Values.Where(a => a.AllTitles.Contains(name, StringComparison.InvariantCultureIgnoreCase))
                .ToList()
        );
}
