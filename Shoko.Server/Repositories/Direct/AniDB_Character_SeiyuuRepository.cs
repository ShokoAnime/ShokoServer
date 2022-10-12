﻿using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Character_SeiyuuRepository : BaseDirectRepository<AniDB_Character_Seiyuu, int>
{
    public AniDB_Character_Seiyuu GetByCharIDAndSeiyuuID(ISession session, int animeid, int catid)
    {
        lock (GlobalDBLock)
        {
            var cr = session
                .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                .Add(Restrictions.Eq("CharID", animeid))
                .Add(Restrictions.Eq("SeiyuuID", catid))
                .UniqueResult<AniDB_Character_Seiyuu>();
            return cr;
        }
    }

    public List<AniDB_Character_Seiyuu> GetByCharID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByCharID(session, id);
        }
    }

    public List<AniDB_Character_Seiyuu> GetByCharID(ISession session, int id)
    {
        lock (GlobalDBLock)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                .Add(Restrictions.Eq("CharID", id))
                .List<AniDB_Character_Seiyuu>();

            return new List<AniDB_Character_Seiyuu>(objs);
        }
    }

    public List<AniDB_Character_Seiyuu> GetBySeiyuuID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var objs = session
                .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                .Add(Restrictions.Eq("SeiyuuID", id))
                .List<AniDB_Character_Seiyuu>();

            return new List<AniDB_Character_Seiyuu>(objs);
        }
    }
}
