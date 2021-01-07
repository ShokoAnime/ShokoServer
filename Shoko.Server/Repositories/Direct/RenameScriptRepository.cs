using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories
{
    public class RenameScriptRepository : BaseDirectRepository<RenameScript, int>
    {
        public RenameScript GetDefaultScript()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                RenameScript cr = session
                    .CreateCriteria(typeof(RenameScript))
                    .Add(Restrictions.Eq("IsEnabledOnImport", 1))
                    .UniqueResult<RenameScript>();
                return cr;
            }
        }

        public RenameScript GetDefaultOrFirst()
        {
            return GetAll().FirstOrDefault();
        }

        public RenameScript GetByName(string scriptName)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                IList<RenameScript> cr = session
                    .CreateCriteria(typeof(RenameScript))
                    .Add(Restrictions.Eq("ScriptName", scriptName))
                    .List<RenameScript>();
                return cr.FirstOrDefault();
            }
        }
    }
}