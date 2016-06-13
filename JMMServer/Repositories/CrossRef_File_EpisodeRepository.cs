using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
    public class CrossRef_File_EpisodeRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Save(CrossRef_File_Episode obj)
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
            logger.Trace("Updating group stats by file from CrossRef_File_EpisodeRepository.Save: {0}", obj.Hash);
            AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
        }

        public CrossRef_File_Episode GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_File_Episode>(id);
            }
        }

        public List<CrossRef_File_Episode> GetByHash(string hash)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_File_Episode))
                    .Add(Restrictions.Eq("Hash", hash))
                    .AddOrder(Order.Asc("EpisodeOrder"))
                    .List<CrossRef_File_Episode>();

                return new List<CrossRef_File_Episode>(xrefs);
            }
        }

        public List<CrossRef_File_Episode> GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, animeID);
            }
        }

        public List<CrossRef_File_Episode> GetByAnimeID(ISession session, int animeID)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_File_Episode))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .List<CrossRef_File_Episode>();

            return new List<CrossRef_File_Episode>(xrefs);
        }

        public List<CrossRef_File_Episode> GetByFileNameAndSize(string filename, long filesize)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var vidfiles = session
                    .CreateCriteria(typeof(CrossRef_File_Episode))
                    .Add(Restrictions.Eq("FileName", filename))
                    .Add(Restrictions.Eq("FileSize", filesize))
                    .List<CrossRef_File_Episode>();

                return new List<CrossRef_File_Episode>(vidfiles);
            }
        }

        /// <summary>
        /// This is the only way to uniquely identify the record other than the IDENTITY
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="episodeID"></param>
        /// <returns></returns>
        public CrossRef_File_Episode GetByHashAndEpisodeID(string hash, int episodeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CrossRef_File_Episode obj = session
                    .CreateCriteria(typeof(CrossRef_File_Episode))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("EpisodeID", episodeID))
                    .UniqueResult<CrossRef_File_Episode>();

                return obj;
            }
        }

        public List<CrossRef_File_Episode> GetByEpisodeID(int episodeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_File_Episode))
                    .Add(Restrictions.Eq("EpisodeID", episodeID))
                    .List<CrossRef_File_Episode>();

                return new List<CrossRef_File_Episode>(xrefs);
            }
        }

        public void Delete(int id)
        {
            int animeID = 0;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_File_Episode cr = GetByID(id);
                    if (cr != null)
                    {
                        animeID = cr.AnimeID;
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }

            if (animeID > 0)
            {
                logger.Trace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {0}", animeID);
                AniDB_Anime.UpdateStatsByAnimeID(animeID);
            }
        }
    }
}