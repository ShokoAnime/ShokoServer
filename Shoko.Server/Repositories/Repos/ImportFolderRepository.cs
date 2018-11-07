using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class ImportFolderRepository : BaseRepository<SVR_ImportFolder, int>
    {

        internal override int SelectKey(SVR_ImportFolder entity) => entity.ImportFolderID;

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }




        public SVR_ImportFolder GetByImportLocation(string importloc)
        {
            importloc = importloc?.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);

            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.FirstOrDefault(a =>
                        !string.IsNullOrEmpty(a.ImportFolderLocation) && a.ImportFolderLocation
                            .Replace('\\', Path.DirectorySeparatorChar)
                            .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                            .Equals(importloc, StringComparison.InvariantCultureIgnoreCase));
                return Table.FirstOrDefault(a =>
                    !string.IsNullOrEmpty(a.ImportFolderLocation) && a.ImportFolderLocation
                        .Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                        .Equals(importloc, StringComparison.InvariantCultureIgnoreCase));
            }

        }

        public List<SVR_ImportFolder> GetByCloudId(int cloudid)
        {
            return Where(a => a.CloudID.HasValue && a.CloudID.Value == cloudid).ToList();
        }
    }
}