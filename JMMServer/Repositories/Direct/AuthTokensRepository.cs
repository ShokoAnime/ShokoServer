using JMMServer.Databases;
using NHibernate.Criterion;
using Shoko.Models.Server;

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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
