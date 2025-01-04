#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Episode_TitleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AniDB_Episode_Title, int>(databaseFactory)
{
    private PocoIndex<int, SVR_AniDB_Episode_Title, int>? _episodeIDs;

    protected override int SelectKey(SVR_AniDB_Episode_Title entity)
        => entity.AniDB_Episode_TitleID;

    public override void PopulateIndexes()
    {
        _episodeIDs = new PocoIndex<int, SVR_AniDB_Episode_Title, int>(Cache, a => a.AniDB_EpisodeID);
    }

    public IReadOnlyList<SVR_AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int episodeID, TitleLanguage language)
        => GetByEpisodeID(episodeID).Where(a => a.Language == language).ToList();

    public IReadOnlyList<SVR_AniDB_Episode_Title> GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeID));
}
