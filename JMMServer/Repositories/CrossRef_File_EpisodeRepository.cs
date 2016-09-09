using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class CrossRef_File_EpisodeRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static PocoCache<int, CrossRef_File_Episode> Cache;
        private static PocoIndex<int, CrossRef_File_Episode, string> Hashes;
        private static PocoIndex<int, CrossRef_File_Episode, int> Animes;
        private static PocoIndex<int, CrossRef_File_Episode, int> Episodes;
        private static PocoIndex<int, CrossRef_File_Episode, string> Filenames;

        public static void InitCache()
        {
            string t = "CrossRef_File_Episode";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            CrossRef_File_EpisodeRepository repo = new CrossRef_File_EpisodeRepository();
            Cache = new PocoCache<int, CrossRef_File_Episode>(repo.InternalGetAll(), a => a.CrossRef_File_EpisodeID);
            Hashes = new PocoIndex<int, CrossRef_File_Episode, string>(Cache, a => a.Hash);
            Animes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache,a=>a.AnimeID);
            Episodes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache,a=>a.EpisodeID);
            Filenames = new PocoIndex<int, CrossRef_File_Episode, string>(Cache,a=>a.FileName);

        }

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
                Cache.Update(obj);
            }
            logger.Trace("Updating group stats by file from CrossRef_File_EpisodeRepository.Save: {0}", obj.Hash);
            AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
        }

        private List<CrossRef_File_Episode> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CrossRef_File_Episode))
                    .List<CrossRef_File_Episode>();

                return new List<CrossRef_File_Episode>(objs);
            }
        }

        public CrossRef_File_Episode GetByID(int id)
        {
            return Cache.Get(id);
        }

        public List<CrossRef_File_Episode> GetByHash(string hash)
        {
            return Hashes.GetMultiple(hash).OrderBy(a => a.EpisodeOrder).ToList();
        }
        public List<CrossRef_File_Episode> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<CrossRef_File_Episode> GetByAnimeID(int animeID)
        {
            return Animes.GetMultiple(animeID);
        }

        public List<CrossRef_File_Episode> GetByAnimeID(ISession session, int animeID)
        {
            return Animes.GetMultiple(animeID);
        }

        public List<CrossRef_File_Episode> GetByFileNameAndSize(string filename, long filesize)
        {
            return Filenames.GetMultiple(filename).Where(a => a.FileSize == filesize).ToList();
        }

        /// <summary>
        /// This is the only way to uniquely identify the record other than the IDENTITY
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="episodeID"></param>
        /// <returns></returns>
        public CrossRef_File_Episode GetByHashAndEpisodeID(string hash, int episodeID)
        {
            return Hashes.GetMultiple(hash).FirstOrDefault(a => a.EpisodeID == episodeID);
        }

        public List<CrossRef_File_Episode> GetByEpisodeID(int episodeID)
        {
            return Episodes.GetMultiple(episodeID);

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
                        Cache.Remove(cr);
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