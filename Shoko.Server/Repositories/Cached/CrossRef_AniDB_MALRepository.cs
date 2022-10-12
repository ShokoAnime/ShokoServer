﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_MALRepository : BaseCachedRepository<CrossRef_AniDB_MAL, int>
{
    private PocoIndex<int, CrossRef_AniDB_MAL, int> _animeIDs;
    private PocoIndex<int, CrossRef_AniDB_MAL, int> _MALIDs;

    public List<CrossRef_AniDB_MAL> GetByAnimeID(int id)
    {
        return ReadLock(() =>
            _animeIDs.GetMultiple(id).OrderBy(a => a.StartEpisodeType).ThenBy(a => a.StartEpisodeNumber).ToList());
    }

    public ILookup<int, CrossRef_AniDB_MAL> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
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
            return EmptyLookup<int, CrossRef_AniDB_MAL>.Instance;
        }

        lock (GlobalDBLock)
        {
            var xrefByAnime = session.CreateCriteria<CrossRef_AniDB_MAL>()
                .Add(Restrictions.In(nameof(CrossRef_AniDB_MAL.AnimeID), animeIds))
                .AddOrder(Order.Asc(nameof(CrossRef_AniDB_MAL.StartEpisodeType)))
                .AddOrder(Order.Asc(nameof(CrossRef_AniDB_MAL.StartEpisodeNumber)))
                .List<CrossRef_AniDB_MAL>()
                .ToLookup(cr => cr.AnimeID);

            return xrefByAnime;
        }
    }

    public List<CrossRef_AniDB_MAL> GetByMALID(int id)
    {
        return ReadLock(() => _MALIDs.GetMultiple(id));
    }

    protected override int SelectKey(CrossRef_AniDB_MAL entity)
    {
        return entity.CrossRef_AniDB_MALID;
    }

    public override void PopulateIndexes()
    {
        _MALIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.MALID);
        _animeIDs = new PocoIndex<int, CrossRef_AniDB_MAL, int>(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
    }
}
