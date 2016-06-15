using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_AniDB_TraktV2Repository
    {
        public void Save(CrossRef_AniDB_TraktV2 obj)
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

        public CrossRef_AniDB_TraktV2 GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_AniDB_TraktV2>(id);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetByAnimeID(ISession session, int id)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Asc("AniDBStartEpisodeType"))
                .AddOrder(Order.Asc("AniDBStartEpisodeNumber"))
                .List<CrossRef_AniDB_TraktV2>();

            return new List<CrossRef_AniDB_TraktV2>(xrefs);
        }

        public List<CrossRef_AniDB_TraktV2> GetByAnimeIDEpTypeEpNumber(ISession session, int id, int aniEpType,
            int aniEpisodeNumber)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                .Add(Restrictions.Eq("AnimeID", id))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .List<CrossRef_AniDB_TraktV2>();

            return new List<CrossRef_AniDB_TraktV2>(xrefs);
        }

        public CrossRef_AniDB_TraktV2 GetByTraktID(ISession session, string id, int season, int episodeNumber,
            int animeID, int aniEpType, int aniEpisodeNumber)
        {
            CrossRef_AniDB_TraktV2 cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                .Add(Restrictions.Eq("TraktID", id))
                .Add(Restrictions.Eq("TraktSeasonNumber", season))
                .Add(Restrictions.Eq("TraktStartEpisodeNumber", episodeNumber))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .UniqueResult<CrossRef_AniDB_TraktV2>();
            return cr;
        }

        public CrossRef_AniDB_TraktV2 GetByTraktID(string id, int season, int episodeNumber, int animeID, int aniEpType,
            int aniEpisodeNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTraktID(session, id, season, episodeNumber, animeID, aniEpType, aniEpisodeNumber);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetByTraktIDAndSeason(string traktID, int season)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                    .Add(Restrictions.Eq("TraktID", traktID))
                    .Add(Restrictions.Eq("TraktSeasonNumber", season))
                    .List<CrossRef_AniDB_TraktV2>();

                return new List<CrossRef_AniDB_TraktV2>(xrefs);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetByTraktID(string traktID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                    .Add(Restrictions.Eq("TraktID", traktID))
                    .List<CrossRef_AniDB_TraktV2>();

                return new List<CrossRef_AniDB_TraktV2>(xrefs);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                    .List<CrossRef_AniDB_TraktV2>();

                return new List<CrossRef_AniDB_TraktV2>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_AniDB_TraktV2 cr = GetByID(id);
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