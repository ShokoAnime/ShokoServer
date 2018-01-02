using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_FileRepository : BaseRepository<SVR_AniDB_File, int,bool> 
        //The bool parameter indicates if we should updateTheAnimeStats on Save/Delete
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AniDB_File, string> Hashes;
        private PocoIndex<int, SVR_AniDB_File, string> SHA1s;
        private PocoIndex<int, SVR_AniDB_File, string> MD5s;
        private PocoIndex<int, SVR_AniDB_File, int> FileIds;
        private PocoIndex<int, SVR_AniDB_File, int> Animes;
        private PocoIndex<int, SVR_AniDB_File, string> Resolutions;
        private PocoIndex<int, SVR_AniDB_File, int> InternalVersions;

        internal override object BeginSave(SVR_AniDB_File entity, SVR_AniDB_File original_entity, bool parameters)
        {
            if (parameters) //UpdateStats
            {
                logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {0}", entity.Hash);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.AnimeID);
            }
            return null;
        }

        internal override void EndDelete(SVR_AniDB_File entity, object returnFromBeginDelete, bool parameters)
        {
            if (entity.AnimeID != 0)
                SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.AnimeID);
        }

        internal override int SelectKey(SVR_AniDB_File entity)
        {
            return entity.FileID;
        }

        internal override void PopulateIndexes()
        {
            // Only populated from main thread before these are accessible, so no lock
            Hashes = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.Hash);
            SHA1s = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.SHA1);
            MD5s = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.MD5);
            FileIds = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.FileID);
            Animes = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.AnimeID);
            Resolutions = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.File_VideoResolution);
            InternalVersions = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.InternalVersion);
        }

        internal override void ClearIndexes()
        {
            InternalVersions = null;
            Resolutions = null;
            Animes = null;
            FileIds = null;
            MD5s = null;
            SHA1s = null;
            Hashes = null;
        }


        public SVR_AniDB_File GetByHash(string hash)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Hashes.GetOne(hash) : Table.FirstOrDefault(a => a.Hash == hash);
            }
        }

        public SVR_AniDB_File GetBySHA1(string hash)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? SHA1s.GetOne(hash) : Table.FirstOrDefault(a=>a.SHA1==hash);
            }
        }

        public SVR_AniDB_File GetByMD5(string hash)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? MD5s.GetOne(hash) : Table.FirstOrDefault(a => a.MD5 == hash);
            }
        }

        public List<SVR_AniDB_File> GetByInternalVersion(int version)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? InternalVersions.GetMultiple(version) : Table.Where(a=>a.InternalVersion==version).ToList();
            }
        }

        public List<SVR_AniDB_File> GetWithWithMissingChapters() //Not Cached
        {
            if (IsCached) // Lock is in GetMany
            {
                List<int> ids = Table.Where(a => a.IsChaptered == -1 && (a.File_FileExtension == "mkv" || a.File_FileExtension == "ogn")).Select(a=>a.FileID).ToList();
                return GetMany(ids);
            }
            return Table.Where(a => a.IsChaptered == -1 && (a.File_FileExtension == "mkv" || a.File_FileExtension == "ogn")).ToList();
        }

        public SVR_AniDB_File GetByHashAndFileSize(string hash, long fsize)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize) : Table.FirstOrDefault(a=>a.Hash==hash && a.FileSize==fsize);
            }
        }

        public SVR_AniDB_File GetByFileID(int fileID)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? FileIds.GetOne(fileID) : Table.FirstOrDefault(a => a.FileID == fileID);
            }
        }


        public List<SVR_AniDB_File> GetByAnimeID(int animeID)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Animes.GetMultiple(animeID) : Table.Where(a => a.AnimeID == animeID).ToList();
            }
        }

        public List<SVR_AniDB_File> GetByResolution(string res)
        {
            using (CacheLock.ReaderLock())
            {
                return IsCached ? Resolutions.GetMultiple(res) : Table.Where(a => a.File_VideoResolution == res).ToList();
            }
        }
    }
}