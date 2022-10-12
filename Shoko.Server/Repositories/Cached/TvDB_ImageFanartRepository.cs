﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class TvDB_ImageFanartRepository : BaseCachedRepository<TvDB_ImageFanart, int>
{
    private PocoIndex<int, TvDB_ImageFanart, int> SeriesIDs;
    private PocoIndex<int, TvDB_ImageFanart, int> TvDBIDs;

    public override void PopulateIndexes()
    {
        SeriesIDs = new PocoIndex<int, TvDB_ImageFanart, int>(Cache, a => a.SeriesID);
        TvDBIDs = new PocoIndex<int, TvDB_ImageFanart, int>(Cache, a => a.Id);
    }

    public TvDB_ImageFanart GetByTvDBID(int id)
    {
        return ReadLock(() => TvDBIDs.GetOne(id));
    }

    public List<TvDB_ImageFanart> GetBySeriesID(int seriesID)
    {
        return ReadLock(() => SeriesIDs.GetMultiple(seriesID));
    }

    public ILookup<int, TvDB_ImageFanart> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeIds == null)
        {
            throw new ArgumentNullException(nameof(animeIds));
        }

        if (animeIds.Length == 0)
        {
            return EmptyLookup<int, TvDB_ImageFanart>.Instance;
        }

        lock (GlobalDBLock)
        {
            var fanartByAnime = session.CreateSQLQuery(@"
                SELECT DISTINCT crAdbTvTb.AniDBID, {tvdbFanart.*}
                   FROM CrossRef_AniDB_TvDB AS crAdbTvTb
                      INNER JOIN TvDB_ImageFanart AS tvdbFanart
                         ON tvdbFanart.SeriesID = crAdbTvTb.TvDBID
                   WHERE crAdbTvTb.AniDBID IN (:animeIds)")
                .AddScalar("AniDBID", NHibernateUtil.Int32)
                .AddEntity("tvdbFanart", typeof(TvDB_ImageFanart))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (TvDB_ImageFanart)r[1]);

            return fanartByAnime;
        }
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(TvDB_ImageFanart entity)
    {
        return entity.TvDB_ImageFanartID;
    }
}
