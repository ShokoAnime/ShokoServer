﻿using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class CrossRef_Languages_AniDB_FileRepository : BaseDirectRepository<CrossRef_Languages_AniDB_File, int>
{
    public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var files = session
                .CreateCriteria(typeof(CrossRef_Languages_AniDB_File))
                .Add(Restrictions.Eq("FileID", id))
                .List<CrossRef_Languages_AniDB_File>();

            return new List<CrossRef_Languages_AniDB_File>(files);
        }
    }
}
