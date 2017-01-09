using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class VersionsRepository : BaseDirectRepository<Versions, int>
    {
        private VersionsRepository()
        { }

        public static VersionsRepository Create()
        {
            return new VersionsRepository();
        }
        public Dictionary<string,Dictionary<string, Versions>> GetAllByType(string vertype)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(Versions)).Add(Restrictions.Eq("VersionType", vertype)).List<Versions>().GroupBy(a=>a.VersionValue ?? "").ToDictionary(a=>a.Key,a=>a.GroupBy(b=>b.VersionRevision ?? "").ToDictionary(b=>b.Key,b=>b.FirstOrDefault()));
            }
        }
    }
}