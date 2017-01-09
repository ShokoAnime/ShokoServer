using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class ScheduledUpdateRepository : BaseDirectRepository<ScheduledUpdate,int>
    {
        private ScheduledUpdateRepository()
        {
        }

        public static ScheduledUpdateRepository Create()
        {
            return new ScheduledUpdateRepository();
        }
        public ScheduledUpdate GetByUpdateType(int uptype)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ScheduledUpdate cr = session
                    .CreateCriteria(typeof(ScheduledUpdate))
                    .Add(Restrictions.Eq("UpdateType", uptype))
                    .UniqueResult<ScheduledUpdate>();
                return cr;
            }
        }
    }
}