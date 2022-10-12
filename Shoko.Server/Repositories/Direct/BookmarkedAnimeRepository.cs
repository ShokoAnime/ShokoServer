﻿using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class BookmarkedAnimeRepository : BaseDirectRepository<BookmarkedAnime, int>
{
    public BookmarkedAnime GetByAnimeID(int animeID)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(BookmarkedAnime))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .UniqueResult<BookmarkedAnime>();
            return cr;
        }
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll()
    {
        return base.GetAll().OrderBy(a => a.Priority).ToList();
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll(ISession session)
    {
        return GetAll();
    }

    public override IReadOnlyList<BookmarkedAnime> GetAll(ISessionWrapper session)
    {
        return GetAll();
    }
}
