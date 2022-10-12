﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class TvDB_ImageWideBannerRepository : BaseCachedRepository<TvDB_ImageWideBanner, int>
{
    private PocoIndex<int, TvDB_ImageWideBanner, int> SeriesIDs;
    private PocoIndex<int, TvDB_ImageWideBanner, int> TvDBIDs;

    public override void PopulateIndexes()
    {
        SeriesIDs = new PocoIndex<int, TvDB_ImageWideBanner, int>(Cache, a => a.SeriesID);
        TvDBIDs = new PocoIndex<int, TvDB_ImageWideBanner, int>(Cache, a => a.Id);
    }

    public override void RegenerateDb()
    {
    }

    protected override int SelectKey(TvDB_ImageWideBanner entity)
    {
        return entity.TvDB_ImageWideBannerID;
    }

    public TvDB_ImageWideBanner GetByTvDBID(int id)
    {
        return ReadLock(() => TvDBIDs.GetOne(id));
    }

    public List<TvDB_ImageWideBanner> GetBySeriesID(int seriesID)
    {
        return ReadLock(() => SeriesIDs.GetMultiple(seriesID));
    }

    public ILookup<int, TvDB_ImageWideBanner> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
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
            return EmptyLookup<int, TvDB_ImageWideBanner>.Instance;
        }

        lock (GlobalDBLock)
        {
            var bannersByAnime = session.CreateSQLQuery(@"
                SELECT DISTINCT crAdbTvTb.AniDBID, {tvdbBanner.*}
                   FROM CrossRef_AniDB_TvDB AS crAdbTvTb
                      INNER JOIN TvDB_ImageWideBanner AS tvdbBanner
                         ON tvdbBanner.SeriesID = crAdbTvTb.TvDBID
                   WHERE crAdbTvTb.AniDBID IN (:animeIds)")
                .AddScalar("AniDBID", NHibernateUtil.Int32)
                .AddEntity("tvdbBanner", typeof(TvDB_ImageWideBanner))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (TvDB_ImageWideBanner)r[1]);

            return bannersByAnime;
        }
    }
}
