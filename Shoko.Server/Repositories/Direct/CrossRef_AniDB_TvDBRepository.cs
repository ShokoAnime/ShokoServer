using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class CrossRef_AniDB_TvDBRepository : BaseDirectRepository<SVR_CrossRef_AniDB_TvDB, int>
    {
        private CrossRef_AniDB_TvDBRepository()
        {
            
        }

        public static CrossRef_AniDB_TvDBRepository Create()
        {
            return new CrossRef_AniDB_TvDBRepository();
        }
        public SVR_CrossRef_AniDB_TvDB GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public SVR_CrossRef_AniDB_TvDB GetByAnimeID(ISession session, int id)
        {
            SVR_CrossRef_AniDB_TvDB cr = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDB))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<SVR_CrossRef_AniDB_TvDB>();
            return cr;
        }

        public SVR_CrossRef_AniDB_TvDB GetByTvDBID(int id, int season)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_CrossRef_AniDB_TvDB cr = session
                    .CreateCriteria(typeof(SVR_CrossRef_AniDB_TvDB))
                    .Add(Restrictions.Eq("TvDBID", id))
                    .Add(Restrictions.Eq("TvDBSeasonNumber", season))
                    .UniqueResult<SVR_CrossRef_AniDB_TvDB>();
                return cr;
            }
        }

    }
}