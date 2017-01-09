using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
using Shoko.Models;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    public class SVR_CrossRef_AniDB_TvDBV2 : CrossRef_AniDB_TvDBV2
    {
      

        public TvDB_Series GetTvDBSeries()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBSeries(session.Wrap());
            }
        }

        public TvDB_Series GetTvDBSeries(ISessionWrapper session)
        {
            return RepoFactory.TvDB_Series.GetByTvDBID(session, TvDBID);
        }

     
    }
}