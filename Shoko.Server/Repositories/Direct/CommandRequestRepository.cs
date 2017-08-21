using System.Collections.Generic;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class CommandRequestRepository : BaseDirectRepository<CommandRequest, int>
    {
        private CommandRequestRepository()
        {
        }

        public static CommandRequestRepository Create()
        {
            return new CommandRequestRepository();
        }

        public CommandRequest GetByCommandID(string cmdid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                IList<CommandRequest> crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages))
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages))
                    .List<CommandRequest>();

                return new List<CommandRequest>(crs);
            }
        }

        public CommandRequest GetNextDBCommandRequestHasher()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                IList<CommandRequest> crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Or(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload),
                        Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages)))
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var crs = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Or(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload),
                        Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages)))
                    .List<CommandRequest>();

                return new List<CommandRequest>(crs);
            }
        }

        public int GetQueuedCommandCountGeneral()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload))
                    .Add(!Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages))
                    .SetProjection(Projections.Count("CommandRequestID"))
                    .UniqueResult();

                return (int) cnt;
            }
        }

        public int GetQueuedCommandCountHasher()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Eq("CommandType", (int) CommandRequestType.HashFile))
                    .SetProjection(Projections.Count("CommandRequestID"))
                    .UniqueResult();

                return (int) cnt;
            }
        }

        public int GetQueuedCommandCountImages()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var cnt = session
                    .CreateCriteria(typeof(CommandRequest))
                    .Add(Restrictions.Or(Restrictions.Eq("CommandType", (int) CommandRequestType.ImageDownload),
                            Restrictions.Eq("CommandType", (int) CommandRequestType.ValidateAllImages)))
                        .SetProjection(Projections.Count("CommandRequestID"))
                        .UniqueResult();

                return (int) cnt;
            }
        }
    }
}