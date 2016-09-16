using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class AuthTokensRepository : BaseDirectRepository<AuthTokens, int>
    {
        private AuthTokensRepository()
        {
            
        }

        public static AuthTokensRepository Create()
        {
            return new AuthTokensRepository();
        }
        public AuthTokens GetByAuthID(int authID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AuthTokens cr = session
                    .CreateCriteria(typeof(AuthTokens))
                    .Add(Restrictions.Eq("AuthID", authID))
                    .UniqueResult<AuthTokens>();
                return cr;
            }
        }
    }
}
