using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CommandRequestRepository : BaseDirectRepository<CommandRequest, int>
    {
        public CommandRequest GetByCommandID(string cmdid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCommandID(session, cmdid);
            }
        }

        public CommandRequest GetByCommandID(ISession session, string cmdid)
        {
            CommandRequest cr = session
                .CreateCriteria(typeof(CommandRequest))
                .Add(Restrictions.Eq("CommandID", cmdid))
                .UniqueResult<CommandRequest>();
            return cr;
        }

      
        public CommandRequest GetNextDBCommandRequestGeneral()
        {
            /*SELECT TOP 1 CommandRequestID
			FROM CommandRequest
			ORDER BY Priority ASC, DateTimeUpdated ASC*/
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                IList<CommandRequest> crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .AddOrder(Order.Asc("Priority"))
                    .AddOrder(Order.Asc("DateTimeUpdated"))
                    .SetMaxResults(1)
                    .List<CommandRequest>();

                if (crs.Count > 0) return crs[0];

                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestGeneral()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .List<CommandRequest>();

                return new List<CommandRequest>(crs);
            }
        }

        public CommandRequest GetNextDBCommandRequestHasher()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                IList<CommandRequest> crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .AddOrder(Order.Asc("Priority"))
                    .AddOrder(Order.Asc("DateTimeUpdated"))
                    .SetMaxResults(1)
                    .List<CommandRequest>();

                if (crs.Count > 0) return crs[0];

                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestHasher()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .List<CommandRequest>();

                return new List<CommandRequest>(crs);
            }
        }

        public CommandRequest GetNextDBCommandRequestImages()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                IList<CommandRequest> crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .AddOrder(Order.Asc("Priority"))
                    .AddOrder(Order.Asc("DateTimeUpdated"))
                    .SetMaxResults(1)
                    .List<CommandRequest>();

                if (crs.Count > 0) return crs[0];

                return null;
            }
        }

        public List<CommandRequest> GetAllCommandRequestImages()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .List<CommandRequest>();

                return new List<CommandRequest>(crs);
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

                return (int) cnt;
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

                return (int) cnt;
            }
        }

        public int GetQueuedCommandCountImages()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

                return (int) cnt;
            }
        }

    }
}