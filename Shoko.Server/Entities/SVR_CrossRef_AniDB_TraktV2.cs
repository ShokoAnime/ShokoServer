using NHibernate;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class SVR_CrossRef_AniDB_TraktV2 : CrossRef_AniDB_TraktV2
    {

        public SVR_CrossRef_AniDB_TraktV2()
        {
        }
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