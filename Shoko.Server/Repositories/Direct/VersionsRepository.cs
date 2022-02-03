using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class VersionsRepository : BaseDirectRepository<Versions, int>
    {

        public Dictionary<string, Dictionary<string, Versions>> GetAllByType(string vertype)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(Versions))
                    .Add(Restrictions.Eq("VersionType", vertype))
                    .List<Versions>()
                    .GroupBy(a => a.VersionValue ?? string.Empty)
                    .ToDictionary(a => a.Key,
                        a => a.GroupBy(b => b.VersionRevision ?? string.Empty).ToDictionary(b => b.Key, b => b.FirstOrDefault()));
            }
        }
    }
}