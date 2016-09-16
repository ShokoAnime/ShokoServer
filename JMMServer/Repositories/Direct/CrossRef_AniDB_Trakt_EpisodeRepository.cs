using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_Trakt_EpisodeRepository : BaseDirectRepository<CrossRef_AniDB_Trakt_Episode, int>
    {
        private CrossRef_AniDB_Trakt_EpisodeRepository()
        {
            
        }

        public static CrossRef_AniDB_Trakt_EpisodeRepository Create()
        {
            return new CrossRef_AniDB_Trakt_EpisodeRepository();
        }
        public CrossRef_AniDB_Trakt_Episode GetByAniDBEpisodeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_Trakt_Episode cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Trakt_Episode))
                    .Add(Restrictions.Eq("AniDBEpisodeID", id))
                    .UniqueResult<CrossRef_AniDB_Trakt_Episode>();
                return cr;
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetByAnimeID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(CrossRef_AniDB_Trakt_Episode))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<CrossRef_AniDB_Trakt_Episode>();

            return new List<CrossRef_AniDB_Trakt_Episode>(objs);
        }

       
    }
}