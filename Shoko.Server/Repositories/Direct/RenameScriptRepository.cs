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
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(RenameScript))
                .Add(Restrictions.Eq("IsEnabledOnImport", 1))
                .UniqueResult<RenameScript>();
            return cr;
        }

        public RenameScript GetDefaultOrFirst()
        {
            // This should list the enabled one first, falling back if none are
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(RenameScript))
                .AddOrder(Order.Desc("IsEnabledOnImport"))
                .AddOrder(Order.Asc("RenameScriptID"))
                .List<RenameScript>().FirstOrDefault();
            return cr;
        }

        public RenameScript GetByName(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName)) return null;
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(RenameScript))
                .Add(Restrictions.Eq("ScriptName", scriptName))
                .List<RenameScript>().FirstOrDefault();
            return cr;
        }
    }
}