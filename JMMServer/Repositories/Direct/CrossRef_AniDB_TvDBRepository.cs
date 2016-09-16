using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_TvDBRepository : BaseDirectRepository<CrossRef_AniDB_TvDB, int>
    {      

        public CrossRef_AniDB_TvDB GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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
            using (var session = JMMService.SessionFactory.OpenSession())
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