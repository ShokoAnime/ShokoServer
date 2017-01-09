using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Repositories.Cached
{
    public class ImportFolderRepository : BaseCachedRepository<SVR_ImportFolder,int>
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
            return Cache.Values.FirstOrDefault(a => a.ImportFolderLocation == importloc || a.ImportFolderLocation == importloc);
        }

        public List<SVR_ImportFolder> GetByCloudId(int cloudid)
        {
            return Cache.Values.Where(a=>a.CloudID.HasValue && a.CloudID.Value==cloudid).ToList();
        }

    }
}