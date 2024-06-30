﻿using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TvDB_Episode_OverrideRepository : BaseCachedRepository<CrossRef_AniDB_TvDB_Episode_Override, int>
{
    private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> AnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> EpisodeIDs;

    public override void PopulateIndexes()
    {
        AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache,
            a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1);
        EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache, a => a.AniDBEpisodeID);
    }

    public CrossRef_AniDB_TvDB_Episode_Override GetByAniDBAndTvDBEpisodeIDs(int anidbID, int tvdbID)
    {
        return ReadLock(() => EpisodeIDs.GetMultiple(anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID));
    }

    public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAniDBEpisodeID(int id)
    {
        return ReadLock(() => EpisodeIDs.GetMultiple(id));
    }

    public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAnimeID(int id)
    {
        return ReadLock(() => AnimeIDs.GetMultiple(id));
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(CrossRef_AniDB_TvDB_Episode_Override entity)
    {
        return entity.CrossRef_AniDB_TvDB_Episode_OverrideID;
    }

    public CrossRef_AniDB_TvDB_Episode_OverrideRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
