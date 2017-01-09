using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class LanguageRepository : BaseDirectRepository<Language,int>
    {
        private LanguageRepository()
        {
            
        }

        public static LanguageRepository Create()
        {
            return new LanguageRepository();
        }
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