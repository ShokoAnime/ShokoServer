using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class ScheduledUpdateRepository : BaseDirectRepository<ScheduledUpdate, int>
    {
        public ScheduledUpdate GetByUpdateType(int uptype)
        {
            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var cr = session
                    .CreateCriteria(typeof(ScheduledUpdate))
                    .Add(Restrictions.Eq("UpdateType", uptype))
                    .UniqueResult<ScheduledUpdate>();
                return cr;
            }
        }
    }
}