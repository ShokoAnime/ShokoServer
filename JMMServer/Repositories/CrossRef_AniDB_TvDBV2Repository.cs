using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_AniDB_TvDBV2Repository
    {
        public void Save(CrossRef_AniDB_TvDBV2 obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public CrossRef_AniDB_TvDBV2 GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_AniDB_TvDBV2>(id);
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetByAnimeID(ISession session, int id)
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
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
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

        public List<CrossRef_AniDB_TvDBV2> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(CrossRef_AniDB_TvDBV2))
                    .List<CrossRef_AniDB_TvDBV2>();

                return new List<CrossRef_AniDB_TvDBV2>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_AniDB_TvDBV2 cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}