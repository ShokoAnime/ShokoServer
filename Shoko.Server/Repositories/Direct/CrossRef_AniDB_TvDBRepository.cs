using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class CrossRef_AniDB_TvDBRepository : BaseDirectRepository<CrossRef_AniDB_TvDB, int>
    {
        private CrossRef_AniDB_TvDBRepository()
        {
            
        }

        public static CrossRef_AniDB_TvDBRepository Create()
        {
            return new CrossRef_AniDB_TvDBRepository();
        }
        public CrossRef_AniDB_TvDB GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public CrossRef_AniDB_TvDB GetByAnimeID(ISession session, int id)
        {
            CrossRef_AniDB_TvDB cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_TvDB))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<CrossRef_AniDB_TvDB>();
            return cr;
        }

        public CrossRef_AniDB_TvDB GetByTvDBID(int id, int season)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_TvDB cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_TvDB))
                    .Add(Restrictions.Eq("TvDBID", id))
                    .Add(Restrictions.Eq("TvDBSeasonNumber", season))
                    .UniqueResult<CrossRef_AniDB_TvDB>();
                return cr;
            }
        }

    }
}