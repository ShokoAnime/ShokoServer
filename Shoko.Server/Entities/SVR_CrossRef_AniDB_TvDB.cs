using System;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Entities
{
    [Obsolete]
    public class SVR_CrossRef_AniDB_TvDB 
    {
        public int CrossRef_AniDB_TvDBID { get; private set; }
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }
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