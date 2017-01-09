using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_TraktRepository : BaseDirectRepository<SVR_CrossRef_AniDB_Trakt, int>
    {
        private CrossRef_AniDB_TraktRepository()
        {
            
        }

        public static CrossRef_AniDB_TraktRepository Create()
        {
            return new CrossRef_AniDB_TraktRepository();
        }
        public SVR_CrossRef_AniDB_Trakt GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public SVR_CrossRef_AniDB_Trakt GetByAnimeID(ISession session, int id)
        {
            SVR_CrossRef_AniDB_Trakt cr = session
                .CreateCriteria(typeof(SVR_CrossRef_AniDB_Trakt))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<SVR_CrossRef_AniDB_Trakt>();
            return cr;
        }

        public SVR_CrossRef_AniDB_Trakt GetByTraktID(string id, int season)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_CrossRef_AniDB_Trakt cr = session
                    .CreateCriteria(typeof(SVR_CrossRef_AniDB_Trakt))
                    .Add(Restrictions.Eq("TraktID", id))
                    .Add(Restrictions.Eq("TraktSeasonNumber", season))
                    .UniqueResult<SVR_CrossRef_AniDB_Trakt>();
                return cr;
            }
        }

        public List<SVR_CrossRef_AniDB_Trakt> GetByTraktID(string id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(SVR_CrossRef_AniDB_Trakt))
                    .Add(Restrictions.Eq("TraktID", id))
                    .List<SVR_CrossRef_AniDB_Trakt>();

                return new List<SVR_CrossRef_AniDB_Trakt>(series);
            }
        }
    }
}