using System;
using JMMServer.Databases;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    [Obsolete]
    public class SVR_CrossRef_AniDB_Trakt 
    {
        public int CrossRef_AniDB_TraktID { get; private set; }
        public int AnimeID { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }

        public Trakt_Show GetByTraktShow()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByTraktShow(session);
            }
        }

        public Trakt_Show GetByTraktShow(ISession session)
        {
            return RepoFactory.Trakt_Show.GetByTraktSlug(session, TraktID);
        }

    }
}