using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class LanguageRepository : BaseDirectRepository<Language,int>
    {
        public Language GetByLanguageName(string lanname)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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