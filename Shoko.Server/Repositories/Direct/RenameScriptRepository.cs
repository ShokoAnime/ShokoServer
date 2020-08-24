using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories
{
    public class RenameScriptRepository : BaseDirectRepository<RenameScript, int>
    {
        public RenameScript GetDefaultEnabledScript()
        {
            return GetAll().FirstOrDefault(a => a.IsEnabledOnImport == 1);
        }

        public RenameScript GetDefaultOrFirst()
        {
            return GetAll().FirstOrDefault(a => a.IsEnabledOnImport == 1) ?? GetAll().FirstOrDefault();
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