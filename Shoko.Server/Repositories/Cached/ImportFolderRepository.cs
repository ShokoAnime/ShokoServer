using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class ImportFolderRepository : BaseCachedRepository<SVR_ImportFolder, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
        
        public SVR_ImportFolder SaveImportFolder(ImportFolder folder)
        {
            SVR_ImportFolder ns;
            if (folder.ImportFolderID > 0)
            {
                // update
                ns = GetByID(folder.ImportFolderID);
                if (ns == null)
                    throw new Exception($"Could not find Import Folder ID: {folder.ImportFolderID}");
            }
            else
            {
                // create
                ns = new SVR_ImportFolder();
            }

            if (string.IsNullOrEmpty(folder.ImportFolderName))
                throw new Exception("Must specify an Import Folder name");

            if (string.IsNullOrEmpty(folder.ImportFolderLocation))
                throw new Exception("Must specify an Import Folder location");

            if (folder.CloudID == 0) folder.CloudID = null;

            if (folder.CloudID == null && !Directory.Exists(folder.ImportFolderLocation))
                throw new Exception("Cannot find Import Folder location");

            if (folder.ImportFolderID == 0)
            {
                SVR_ImportFolder nsTemp =
                    GetByImportLocation(folder.ImportFolderLocation);
                if (nsTemp != null)
                    throw new Exception("Another entry already exists for the specified Import Folder location");
            }

            if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                throw new Exception("A folder cannot be a drop source and a drop destination at the same time");

            // check to make sure we don't have multiple drop folders
            IReadOnlyList<SVR_ImportFolder> allFolders = GetAll();

            if (folder.IsDropDestination == 1)
            {
                foreach (SVR_ImportFolder imf in allFolders)
                {
                    if (folder.CloudID == imf.CloudID && imf.IsDropDestination == 1 &&
                        (folder.ImportFolderID == 0 || folder.ImportFolderID != imf.ImportFolderID))
                    {
                        imf.IsDropDestination = 0;
                        Save(imf);
                    }
                    else if (imf.CloudID != folder.CloudID)
                    {
                        if (folder.IsDropSource == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                            throw new Exception("A drop folders cannot have different file systems");

                        if (folder.IsDropDestination == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                        {
                            throw new Exception("A drop folders cannot have different file systems");
                        }
                    }
                }
            }

            ns.ImportFolderName = folder.ImportFolderName;
            ns.ImportFolderLocation = folder.ImportFolderLocation;
            ns.IsDropDestination = folder.IsDropDestination;
            ns.IsDropSource = folder.IsDropSource;
            ns.IsWatched = folder.IsWatched;
            ns.ImportFolderType = folder.ImportFolderType;
            ns.CloudID = folder.CloudID;

            Save(ns);
            
            Utils.MainThreadDispatch(() => { ServerInfo.Instance.RefreshImportFolders(); });
            ShokoServer.StopWatchingFiles();
            ShokoServer.StartWatchingFiles();

            return ns;
        }
    }
}
