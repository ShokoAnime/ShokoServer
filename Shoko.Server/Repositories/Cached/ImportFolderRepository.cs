using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Models.Server;
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
            var repo = new ImportFolderRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
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
