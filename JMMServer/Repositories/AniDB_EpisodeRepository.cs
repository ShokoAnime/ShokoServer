using System.Collections.Generic;
using System.Linq;
using AniDBAPI;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_EpisodeRepository
    {

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static PocoCache<int, AniDB_Episode> Cache;
        private static PocoIndex<int, AniDB_Episode, int> EpisodesIds;
        private static PocoIndex<int, AniDB_Episode, int> Animes;
        public static void InitCache()
        {
            string t = "AniDB_Episodes";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            AniDB_EpisodeRepository repo = new AniDB_EpisodeRepository();
            Cache = new PocoCache<int, AniDB_Episode>(repo.InternalGetAll(), a => a.AniDB_EpisodeID);
            EpisodesIds = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.EpisodeID);
            Animes = new PocoIndex<int, AniDB_Episode, int>(Cache, a => a.AnimeID);
        }
        internal List<AniDB_Episode> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(objs);
            }
        }

        public void Save(AniDB_Episode obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Cache.Update(obj);
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public AniDB_Episode GetByID(int id)
        {
            return Cache.Get(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Episode>(id);
            }*/
        }

        public AniDB_Episode GetByEpisodeID(int id)
        {
            return EpisodesIds.GetOne(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Episode cr = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("EpisodeID", id))
                    .UniqueResult<AniDB_Episode>();
                return cr;
            }*/
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            return Animes.GetMultiple(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }*/
        }

        public List<AniDB_Episode> GetByAnimeID(ISession session, int id)
        {
            return Animes.GetMultiple(id);
            /*
            var eps = session
                .CreateCriteria(typeof(AniDB_Episode))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Episode>();

            return new List<AniDB_Episode>(eps);*/
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            return Animes.GetMultiple(animeid).Where(a=>a.EpisodeNumber==epnumber && a.EpisodeTypeEnum==enEpisodeType.Episode).ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) enEpisodeType.Episode))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, enEpisodeType epType, int epnumber)
        {
            return Animes.GetMultiple(animeid).Where(a => a.EpisodeNumber == epnumber && a.EpisodeTypeEnum == epType).ToList();
/*            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) epType))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            return
                new CrossRef_File_EpisodeRepository().GetAll()
                    .GroupBy(a => a.EpisodeID)
                    .Where(a => a.Count() > 1)
                    .Select(a => GetByEpisodeID(a.Key))
                    .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps =
                    session.CreateQuery(
                        "FROM AniDB_Episode x WHERE x.EpisodeID IN (Select xref.EpisodeID FROM CrossRef_File_Episode xref GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)")
                        .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }*/
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Episode cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}