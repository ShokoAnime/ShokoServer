﻿using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class ScanFileRepository : BaseDirectRepository<ScanFile, int>
{
    public List<ScanFile> GetWaiting(int scanid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting))
                .AddOrder(Order.Asc("CheckDate"))
                .List<ScanFile>()
                .ToList();
        }
    }

    public List<ScanFile> GetByScanID(int scanid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .List<ScanFile>()
                .ToList();
        }
    }

    public List<ScanFile> GetWithError(int scanid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Gt("Status", (int)ScanFileStatus.ProcessedOK))
                .AddOrder(Order.Asc("CheckDate"))
                .List<ScanFile>()
                .ToList();
        }
    }

    public int GetWaitingCount(int scanid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return (int)session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting))
                .SetProjection(Projections.Count("ScanFileID"))
                .UniqueResult();
        }
    }
}
