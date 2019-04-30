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
        
        public CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract)
        {
            CL_Response<ImportFolder> response = new CL_Response<ImportFolder>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                SVR_ImportFolder ns = null;
                if (contract.ImportFolderID > 0)
                {
                    // update
                    ns = GetByID(contract.ImportFolderID);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " +
                                                contract.ImportFolderID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    ns = new SVR_ImportFolder();
                }

                if (string.IsNullOrEmpty(contract.ImportFolderName))
                {
                    response.ErrorMessage = "Must specify an Import Folder name";
                    return response;
                }

                if (string.IsNullOrEmpty(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Must specify an Import Folder location";
                    return response;
                }

                if (contract.CloudID == 0) contract.CloudID = null;

                if (contract.CloudID == null && !Directory.Exists(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Cannot find Import Folder location";
                    return response;
                }

                if (contract.ImportFolderID == 0)
                {
                    SVR_ImportFolder nsTemp =
                        GetByImportLocation(contract.ImportFolderLocation);
                    if (nsTemp != null)
                    {
                        response.ErrorMessage = "An entry already exists for the specified Import Folder location";
                        return response;
                    }
                }

                if (contract.IsDropDestination == 1 && contract.IsDropSource == 1)
                {
                    response.ErrorMessage = "A folder cannot be a drop source and a drop destination at the same time";
                    return response;
                }

                // check to make sure we don't have multiple drop folders
                IReadOnlyList<SVR_ImportFolder> allFolders = GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (SVR_ImportFolder imf in allFolders)
                    {
                        if (contract.CloudID == imf.CloudID && imf.IsDropDestination == 1 &&
                            (contract.ImportFolderID == 0 || contract.ImportFolderID != imf.ImportFolderID))
                        {
                            imf.IsDropDestination = 0;
                            Save(imf);
                        }
                        else if (imf.CloudID != contract.CloudID)
                        {
                            if (contract.IsDropSource == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                            {
                                response.ErrorMessage = "A drop folders cannot have different file systems";
                                return response;
                            }

                            if (contract.IsDropDestination == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                            {
                                response.ErrorMessage = "A drop folders cannot have different file systems";
                                return response;
                            }
                        }
                    }
                }

                ns.ImportFolderName = contract.ImportFolderName;
                ns.ImportFolderLocation = contract.ImportFolderLocation;
                ns.IsDropDestination = contract.IsDropDestination;
                ns.IsDropSource = contract.IsDropSource;
                ns.IsWatched = contract.IsWatched;
                ns.ImportFolderType = contract.ImportFolderType;
                ns.CloudID = contract.CloudID;

                Save(ns);

                response.Result = ns;
                Utils.MainThreadDispatch(() => { ServerInfo.Instance.RefreshImportFolders(); });
                ShokoServer.StopWatchingFiles();
                ShokoServer.StartWatchingFiles();

                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }
    }
}
