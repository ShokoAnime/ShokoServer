using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories
{
    public class AniDB_FileRepository : BaseCachedRepository<SVR_AniDB_File, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AniDB_File, string> Hashes;
        private PocoIndex<int, SVR_AniDB_File, string> SHA1s;
        private PocoIndex<int, SVR_AniDB_File, string> MD5s;
        private PocoIndex<int, SVR_AniDB_File, int> FileIds;
        private PocoIndex<int, SVR_AniDB_File, int> Animes;
        private PocoIndex<int, SVR_AniDB_File, string> Resolutions;
        private PocoIndex<int, SVR_AniDB_File, int> InternalVersions;

        private AniDB_FileRepository()
        {
            EndDeleteCallback = (cr) =>
            {
                if (cr.AnimeID > 0)
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.AnimeID);
            };
        }

        public static AniDB_FileRepository Create()
        {
            var repo = new AniDB_FileRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(SVR_AniDB_File entity)
        {
            return entity.AniDB_FileID;
        }

        public override void PopulateIndexes()
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

        public override void RegenerateDb()
        {
        }

        public override void Save(SVR_AniDB_File obj)
        {
            Save(obj, true);
        }

        public void Save(SVR_AniDB_File obj, bool updateStats)
        {
            if (obj.Anime_GroupName == null)
                obj.Anime_GroupName = "UNKNOWN";
            if (obj.Anime_GroupNameShort==null)
                obj.Anime_GroupNameShort = "UNKNOWN";
            base.Save(obj);
            if (updateStats)
            {
                logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {0}", obj.Hash);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
            }
        }


        public SVR_AniDB_File GetByHash(string hash)
        {
            lock (Cache)
            {
                return Hashes.GetOne(hash);
            }
        }

        public SVR_AniDB_File GetBySHA1(string hash)
        {
            lock (Cache)
            {
                return SHA1s.GetOne(hash);
            }
        }

        public SVR_AniDB_File GetByMD5(string hash)
        {
            lock (Cache)
            {
                return MD5s.GetOne(hash);
            }
        }

        public List<SVR_AniDB_File> GetByInternalVersion(int version)
        {
            lock (Cache)
            {
                return InternalVersions.GetMultiple(version);
            }
        }

        public List<SVR_AniDB_File> GetWithWithMissingChapters()
        {
            lock (globalDBLock)
            {
                // the only containers that support chapters (and will have data on anidb)
                List<SVR_AniDB_File> list = DatabaseFactory.SessionFactory.OpenSession()
                    .CreateSQLQuery(
                        @"SELECT FileID FROM AniDB_File WHERE IsChaptered = -1 AND (File_FileExtension = 'mkv' OR File_FileExtension = 'ogm')")
                    .List<int>()
                    .Select(GetByFileID)
                    .ToList();
                return list;
            }
        }

        public SVR_AniDB_File GetByHashAndFileSize(string hash, long fsize)
        {
            lock (Cache)
            {
                return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
            }
        }

        public SVR_AniDB_File GetByFileID(int fileID)
        {
            lock (Cache)
            {
                return FileIds.GetOne(fileID);
            }
        }


        public List<SVR_AniDB_File> GetByAnimeID(int animeID)
        {
            lock (Cache)
            {
                return Animes.GetMultiple(animeID);
            }
        }

        public List<SVR_AniDB_File> GetByResolution(string res)
        {
            lock (Cache)
            {
                return Resolutions.GetMultiple(res);
            }
        }
    }
}
