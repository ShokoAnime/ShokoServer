using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class VersionsRepository : BaseDirectRepository<Versions, int>
    {
        public Versions GetByVersionType(string vertype)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(Versions)).Add(Restrictions.Eq("VersionType", vertype)).UniqueResult<Versions>();
            }
        }
    }
}