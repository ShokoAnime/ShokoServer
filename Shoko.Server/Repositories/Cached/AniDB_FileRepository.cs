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
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
            if (!updateStats) return;
            Logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {Hash}", obj.Hash);
            var anime = RepoFactory.CrossRef_File_Episode.GetByHash(obj.Hash).Select(a => a.AnimeID).Distinct();
            anime.ForEach(SVR_AniDB_Anime.UpdateStatsByAnimeID);
        }


        public SVR_AniDB_File GetByHash(string hash)
        {
            Lock.EnterReadLock();
            var result = Hashes.GetOne(hash);
            Lock.ExitReadLock();
            return result;
        }

        public List<SVR_AniDB_File> GetByInternalVersion(int version)
        {
            Lock.EnterReadLock();
            var result = InternalVersions.GetMultiple(version);
            Lock.ExitReadLock();
            return result;
        }

        public List<SVR_AniDB_File> GetWithWithMissingChapters()
        {
            lock (GlobalDBLock)
            {
                // the only containers that support chapters (and will have data on anidb)
                // < 0 instead of = -1 to avoid any issues with bit data types
                var list = DatabaseFactory.SessionFactory.OpenSession().Query<SVR_AniDB_File>().Where(a => !a.IsChaptered && (a.FileName.EndsWith(".mkv") || a.FileName.EndsWith(".ogm"))).ToList();
                return list;
            }
        }

        public SVR_AniDB_File GetByHashAndFileSize(string hash, long fsize)
        {
            Lock.EnterReadLock();
            var list = Hashes.GetMultiple(hash);
            Lock.ExitReadLock();
            return list.Count == 1 ? list.FirstOrDefault() : list.FirstOrDefault(a => a.FileSize == fsize);
        }

        public SVR_AniDB_File GetByFileID(int fileID)
        {
            Lock.EnterReadLock();
            var result = FileIds.GetOne(fileID);
            Lock.ExitReadLock();
            return result;
        }
    }
}
