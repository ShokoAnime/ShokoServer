using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories
{
    public class RenameScriptRepository : BaseDirectRepository<RenameScript,int>
    {
        private RenameScriptRepository()
        {
            
        }

        public static RenameScriptRepository Create()
        {
            return new RenameScriptRepository();
        }
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

    }
}