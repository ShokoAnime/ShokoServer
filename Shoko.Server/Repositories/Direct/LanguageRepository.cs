using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class LanguageRepository : BaseDirectRepository<Language, int>
    {
        public Language GetByLanguageName(string lanname)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Language cr = session
                    .CreateCriteria(typeof(Language))
                    .Add(Restrictions.Eq("LanguageName", lanname))
                    .UniqueResult<Language>();
                return cr;
            }
        }
    }
}