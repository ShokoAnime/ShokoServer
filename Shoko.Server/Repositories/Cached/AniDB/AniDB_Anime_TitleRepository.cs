#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_TitleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AniDB_Anime_Title, int>(databaseFactory)
{
    private PocoIndex<int, SVR_AniDB_Anime_Title, int>? _animeIDs;

    protected override int SelectKey(SVR_AniDB_Anime_Title entity)
        => entity.AniDB_Anime_TitleID;

    public override void PopulateIndexes()
    {
        _animeIDs = new PocoIndex<int, SVR_AniDB_Anime_Title, int>(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
        // Don't need lock in init
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(SVR_AniDB_Anime_Title)} DbRegen...";
        var titles = Cache.Values.Where(title => title.Title.Contains('`')).ToList();
        foreach (var title in titles)
        {
            title.Title = title.Title.Replace('`', '\'');
            Save(title);
        }
    }

    public List<SVR_AniDB_Anime_Title> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));
}
