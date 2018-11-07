using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_File_EpisodeRepository : BaseRepository<CrossRef_File_Episode, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, CrossRef_File_Episode, string> Hashes;
        private PocoIndex<int, CrossRef_File_Episode, int> Animes;
        private PocoIndex<int, CrossRef_File_Episode, int> Episodes;
        private PocoIndex<int, CrossRef_File_Episode, string> Filenames;

        internal override int SelectKey(CrossRef_File_Episode entity) => entity.CrossRef_File_EpisodeID;

        internal override void PopulateIndexes()
        {
            Hashes = new PocoIndex<int, CrossRef_File_Episode, string>(Cache, a => a.Hash);
            Animes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache, a => a.AnimeID);
            Episodes = new PocoIndex<int, CrossRef_File_Episode, int>(Cache, a => a.EpisodeID);
            Filenames = new PocoIndex<int, CrossRef_File_Episode, string>(Cache, a => a.FileName);
        }

        internal override void ClearIndexes()
        {
            Hashes = null;
            Animes = null;
            Episodes = null;
            Filenames = null;
        }

        internal override void EndSave(CrossRef_File_Episode entity, object returnFromBeginSave,
            object parameters)
        {
            logger.Trace("Updating group stats by file from CrossRef_File_EpisodeRepository.Save: {0}", entity.Hash);
            SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.AnimeID);

        }

        internal override void EndDelete(CrossRef_File_Episode entity, object returnFromBeginDelete, object parameters)
        {
            if (entity != null && entity.AnimeID !=0)
            {
                logger.Trace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {0}",entity.AnimeID);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.AnimeID);
            }
        }

        public List<CrossRef_File_Episode> GetByHash(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).OrderBy(a => a.EpisodeOrder).ToList();
                return Table.Where(a => a.Hash == hash).OrderBy(a => a.EpisodeOrder).ToList();
            }
        }
        public List<int> GetIdsByHash(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).Select(a=>a.CrossRef_File_EpisodeID).ToList();
                return Table.Where(a => a.Hash == hash).Select(a => a.CrossRef_File_EpisodeID).ToList();
            }
        }
        public List<int> GetAnimesIdByHashes(IEnumerable<string> hashes)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return hashes.SelectMany(a=>Hashes.GetMultiple(a)).Select(a => a.AnimeID).Distinct().ToList();
                return Table.Where(a => hashes.Contains(a.Hash)).Select(a => a.AnimeID).Distinct().ToList();
            }
        }
        public List<int> GetMultiEpIdByHashes(IEnumerable<string> hashes)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return hashes.Select(a=>Hashes.GetMultiple(a)).SelectMany(a=>a).GroupBy(a=>a.EpisodeID).Where(a=>a.Count()>1).Select(a=>a.Key).Distinct().ToList();
                return Table.Where(a => hashes.Contains(a.Hash)).GroupBy(a => a.EpisodeID).Where(a => a.Count() > 1).Select(a => a.Key).Distinct().ToList();
            }
        }
        public List<CrossRef_File_Episode> GetByAnimeID(int animeID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(animeID);
                return Table.Where(a => a.AnimeID == animeID).ToList();
            }
        }


        public List<CrossRef_File_Episode> GetByFileNameAndSize(string filename, long filesize)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Filenames.GetMultiple(filename).Where(a => a.FileSize == filesize).ToList();
                return Table.Where(a => a.FileName==filename && a.FileSize==filesize).ToList();
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
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).FirstOrDefault(a => a.EpisodeID == episodeID);
                return Table.FirstOrDefault(a => a.Hash == hash && a.EpisodeID == episodeID);
            }
        }

        public List<CrossRef_File_Episode> GetByEpisodeID(int episodeID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Episodes.GetMultiple(episodeID);
                return Table.Where(a => a.EpisodeID==episodeID).ToList();
            }
        }
        public List<string> GetHashesByEpisodeIds(IEnumerable<int> episodeIDs)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return episodeIDs.SelectMany(a => Episodes.GetMultiple(a)).Select(a => a.Hash).Distinct().ToList();
                return Table.Where(a => episodeIDs.Contains(a.EpisodeID)).Select(a => a.Hash).Distinct().ToList();
            }
        }
        public List<string> GetHashesByEpisodeId(int episodeID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    Episodes.GetMultiple(episodeID).Select(a => a.Hash).Distinct().ToList();
                return Table.Where(a => a.EpisodeID==episodeID).Select(a => a.Hash).Distinct().ToList();
            }
        }

        public List<int> GetEpisodesIdsWithMultipleFiles()
        {
            using (RepoLock.ReaderLock())
            {
                return WhereAll().GroupBy(a => a.EpisodeID).Where(a => a.Count() > 1).Select(a => a.Key).ToList();
            }
        }

        public Dictionary<int, List<string>> GetGroupByEpisodeIDHashes()
        {
            using (RepoLock.ReaderLock())
            {
                return WhereAll().GroupBy(a => a.EpisodeID).ToDictionary(a => a.Key, a => a.Select(b => b.Hash).ToList());
            }
        }
    }
}