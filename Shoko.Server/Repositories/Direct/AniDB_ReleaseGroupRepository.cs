﻿using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_ReleaseGroupRepository : BaseDirectRepository<AniDB_ReleaseGroup, int>
{
    public AniDB_ReleaseGroup GetByGroupID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(AniDB_ReleaseGroup))
                .Add(Restrictions.Eq("GroupID", id))
                .UniqueResult<AniDB_ReleaseGroup>();
            return cr;
        }
    }
}
