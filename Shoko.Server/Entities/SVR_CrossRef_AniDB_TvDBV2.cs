using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Entities
{
    public class SVR_CrossRef_AniDB_TvDBV2 : CrossRef_AniDB_TvDBV2
    {
        public SVR_CrossRef_AniDB_TvDBV2()
        {
        }

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