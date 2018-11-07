using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class FileNameHashRepository : BaseRepository<FileNameHash, int>
    {
        private PocoIndex<int, FileNameHash, string> Hashes;
        private PocoIndex<int, FileNameHash, string, long> FileNameFileSizes;


        internal override int SelectKey(FileNameHash entity) => entity.FileNameHashID;
        

        internal override void PopulateIndexes()
        {
            Hashes = new PocoIndex<int, FileNameHash, string>(Cache, a => a.Hash);
            FileNameFileSizes = new PocoIndex<int, FileNameHash, string, long>(Cache, a => a.FileName, a => a.FileSize);            
        }
        internal override void ClearIndexes()
        {
            Hashes = null;
            FileNameFileSizes = null;
        }


        public List<FileNameHash> GetByHash(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash);
                return Table.Where(a => a.Hash == hash).ToList();
            }
        }

        public List<FileNameHash> GetByFileNameAndSize(string filename, long filesize)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return FileNameFileSizes.GetMultiple(filename, filesize);
                return Table.Where(a => a.FileName==filename && a.FileSize==filesize).ToList();
            }

        }

        public FileNameHash GetByNameSizeAndHash(string filename, long filesize, string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).FirstOrDefault(a=>a.FileName==filename && a.FileSize==filesize);
                return Table.Where(a => a.Hash == hash).FirstOrDefault(a => a.FileName == filename && a.FileSize == filesize);
            }
        }
    }
}