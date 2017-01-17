using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories
{
    public class CrossRef_File_EpisodeRepository : BaseCachedRepository<CrossRef_File_Episode, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, CrossRef_File_Episode, string> Hashes;
        private PocoIndex<int, CrossRef_File_Episode, int> Animes;
        private PocoIndex<int, CrossRef_File_Episode, int> Episodes;
        private PocoIndex<int, CrossRef_File_Episode, string> Filenames;

        public override void PopulateIndexes()
        {
            Hashes = new PocoIndex<int, CrossRef_File_Episode, string>(Cache, a => a.Hash);
            Animes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache, a => a.AnimeID);
            Episodes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache, a => a.EpisodeID);
            Filenames = new PocoIndex<int, CrossRef_File_Episode, string>(Cache, a => a.FileName);
        }

        public override void RegenerateDb()
        {

        }

        private CrossRef_File_EpisodeRepository()
        {
            EndSaveCallback = (obj) =>
            {
                logger.Trace("Updating group stats by file from CrossRef_File_EpisodeRepository.Save: {0}", obj.Hash);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
            };
            EndDeleteCallback = (obj) =>
            {
                if (obj!=null && obj.AnimeID > 0)
                {
                    logger.Trace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {0}", obj.AnimeID);
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
                }
            };
        }

        public static CrossRef_File_EpisodeRepository Create()
        {
            return new CrossRef_File_EpisodeRepository();
        }

        protected override int SelectKey(CrossRef_File_Episode entity)
        {
            return entity.CrossRef_File_EpisodeID;
        }

        public List<CrossRef_File_Episode> GetByHash(string hash)
        {
            return Hashes.GetMultiple(hash).OrderBy(a => a.EpisodeOrder).ToList();
        }


        public List<CrossRef_File_Episode> GetByAnimeID(int animeID)
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
    }
}