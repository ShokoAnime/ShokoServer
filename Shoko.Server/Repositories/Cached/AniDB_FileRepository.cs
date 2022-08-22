using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories
{
    public class AniDB_FileRepository : BaseCachedRepository<SVR_AniDB_File, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AniDB_File, string> Hashes;
        private PocoIndex<int, SVR_AniDB_File, int> FileIds;
        private PocoIndex<int, SVR_AniDB_File, int> InternalVersions;

        protected override int SelectKey(SVR_AniDB_File entity)
        {
            return entity.AniDB_FileID;
        }

        public override void PopulateIndexes()
        {
            // Only populated from main thread before these are accessible, so no lock
            Hashes = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.Hash);
            FileIds = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.FileID);
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
            base.Save(obj);
            if (updateStats)
            {
                logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {0}", obj.Hash);
                var anime = RepoFactory.CrossRef_File_Episode.GetByHash(obj.Hash).Select(a => a.AnimeID).Distinct();
                anime.ForEach(SVR_AniDB_Anime.UpdateStatsByAnimeID);
            }
        }


        public SVR_AniDB_File GetByHash(string hash)
        {
            lock (Cache)
            {
                return Hashes.GetOne(hash);
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
                // < 0 instead of = -1 to avoid any issues with bit data types
                List<SVR_AniDB_File> list = DatabaseFactory.SessionFactory.OpenSession()
                    .CreateSQLQuery(
                        @"SELECT FileID FROM AniDB_File WHERE IsChaptered IS NULL OR IsChaptered < 0 AND (FileName LIKE '%.mkv' OR FileName LIKE '%.ogm')")
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
                var list = Hashes.GetMultiple(hash);
                return list.Count == 1 ? list.First() : list.FirstOrDefault(a => a.FileSize == fsize);
            }
        }

        public SVR_AniDB_File GetByFileID(int fileID)
        {
            lock (Cache)
            {
                return FileIds.GetOne(fileID);
            }
        }
    }
}
