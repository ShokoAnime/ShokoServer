using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class FileFfdshowPresetRepository : BaseRepository<FileFfdshowPreset, int>
    {
        private PocoIndex<int, FileFfdshowPreset, string, long> HashFileSizes;

        internal override int SelectKey(FileFfdshowPreset entity) => entity.FileFfdshowPresetID;
        

        internal override void PopulateIndexes()
        {
            HashFileSizes = new PocoIndex<int, FileFfdshowPreset, string, long>(Cache, a => a.Hash,a=>a.FileSize);
        }

        internal override void ClearIndexes()
        {
            HashFileSizes = null;
        }

        public FileFfdshowPreset GetByHashAndSize(string hash, long fsize)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return HashFileSizes.GetOne(hash, fsize);
                return Table.FirstOrDefault(a => a.Hash == hash && a.FileSize == fsize);
            }
        }
    }
}