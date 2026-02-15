using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Anime_TitleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Anime_Title, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_Title, int>? _animeIDs;

    protected override int SelectKey(AniDB_Anime_Title entity)
        => entity.AniDB_Anime_TitleID;

    public override void PopulateIndexes()
    {
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
        // Don't need lock in init
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(AniDB_Anime_Title)} DbRegen...";
        var titles = Cache.Values.Where(title => title.Title.Contains('`')).ToList();
        foreach (var title in titles)
        {
            title.Title = title.Title.Replace('`', '\'');
            Save(title);
        }
    }

    public List<AniDB_Anime_Title> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));
}
