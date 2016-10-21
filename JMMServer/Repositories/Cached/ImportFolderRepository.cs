using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;

namespace JMMServer.Repositories.Cached
{
    public class ImportFolderRepository : BaseCachedRepository<ImportFolder,int>
    {
        private ImportFolderRepository()
        {
        }

        protected override int SelectKey(ImportFolder entity)
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
        public ImportFolder GetByImportLocation(string importloc)
        {
            return Cache.Values.FirstOrDefault(a => a.ImportFolderLocation == importloc || a.ParsedImportFolderLocation==importloc);
        }

        public List<ImportFolder> GetByCloudId(int cloudid)
        {
            return Cache.Values.Where(a=>a.CloudID.HasValue && a.CloudID.Value==cloudid).ToList();
        }

    }
}