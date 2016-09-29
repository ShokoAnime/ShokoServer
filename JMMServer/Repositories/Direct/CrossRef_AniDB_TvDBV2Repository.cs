using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_TvDBV2Repository : BaseDirectRepository<CrossRef_AniDB_TvDBV2, int>
    {
        private CrossRef_AniDB_TvDBV2Repository()
        {
            
        }

        public static CrossRef_AniDB_TvDBV2Repository Create()
        {
            return new CrossRef_AniDB_TvDBV2Repository();
        }
        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(ISessionWrapper session, int id)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Asc("AniDBStartEpisodeType"))
                .AddOrder(Order.Asc("AniDBStartEpisodeNumber"))
                .List<CrossRef_AniDB_TvDBV2>();

            return new List<CrossRef_AniDB_TvDBV2>(xrefs);
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeIDEpTypeEpNumber(ISession session, int id, int aniEpType,
            int aniEpisodeNumber)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Le("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .List<CrossRef_AniDB_TvDBV2>();

            return new List<CrossRef_AniDB_TvDBV2>(xrefs);
        }

        public CrossRef_AniDB_TvDBV2 GetByTvDBID(ISession session, int id, int season, int episodeNumber, int animeID,
            int aniEpType, int aniEpisodeNumber)
        {
            CrossRef_AniDB_TvDBV2 cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_TvDBV2))
                .Add(Restrictions.Eq("TvDBID", id))
                .Add(Restrictions.Eq("TvDBSeasonNumber", season))
                .Add(Restrictions.Eq("TvDBStartEpisodeNumber", episodeNumber))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .UniqueResult<CrossRef_AniDB_TvDBV2>();
            return cr;
        }

        public CrossRef_AniDB_TvDBV2 GetByTvDBID(int id, int season, int episodeNumber, int animeID, int aniEpType,
            int aniEpisodeNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTvDBID(session, id, season, episodeNumber, animeID, aniEpType, aniEpisodeNumber);
            }
        }
     
    }
}