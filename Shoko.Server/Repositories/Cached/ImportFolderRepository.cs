using System;
using System.Collections.Generic;
using System.Linq;
using Pri.LongPath;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class ImportFolderRepository : BaseCachedRepository<SVR_ImportFolder, int>
    {
        private ImportFolderRepository()
        {
        }

        protected override int SelectKey(SVR_ImportFolder entity)
        {
            return entity.ImportFolderID;
        }

        public override void PopulateIndexes()
        {
        }

        public override void RegenerateDb()
        {
        }


        public static ImportFolderRepository Create()
        {
            return new ImportFolderRepository();
        }

        public SVR_ImportFolder GetByImportLocation(string importloc)
        {
            lock (Cache)
            {
                return Cache.Values.FirstOrDefault(a =>
                    a.ImportFolderLocation?.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                        .Equals(
                            importloc?.Replace('\\', Path.DirectorySeparatorChar)
                                .Replace('/', Path.DirectorySeparatorChar)
                                .TrimEnd(Path.DirectorySeparatorChar),
                            StringComparison.InvariantCultureIgnoreCase) ?? false);
            }
        }

        public List<SVR_ImportFolder> GetByCloudId(int cloudid)
        {
            lock (Cache)
            {
                return Cache.Values.Where(a => a.CloudID.HasValue && a.CloudID.Value == cloudid).ToList();
            }
        }
    }
}