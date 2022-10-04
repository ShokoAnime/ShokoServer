using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories;

public class AniDB_Episode_TitleRepository : BaseCachedRepository<SVR_AniDB_Episode_Title, int>
{
    private PocoIndex<int, SVR_AniDB_Episode_Title, int> Episodes;

    public override void PopulateIndexes()
    {
        Episodes = new PocoIndex<int, SVR_AniDB_Episode_Title, int>(Cache, a => a.AniDB_EpisodeID);
    }

    protected override int SelectKey(SVR_AniDB_Episode_Title entity)
    {
        return entity.AniDB_Episode_TitleID;
    }

    public override void RegenerateDb()
    {
    }

    public List<SVR_AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int id, TitleLanguage language)
    {
        return GetByEpisodeID(id).Where(a => a.Language == language).ToList();
    }

    public List<SVR_AniDB_Episode_Title> GetByEpisodeID(int id)
    {
        return ReadLock(() => Episodes.GetMultiple(id));
    }
}
