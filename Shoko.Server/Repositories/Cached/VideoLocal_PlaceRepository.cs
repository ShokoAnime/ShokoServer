using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocal_PlaceRepository : BaseCachedRepository<SVR_VideoLocal_Place, int>
    {
        private PocoIndex<int, SVR_VideoLocal_Place, int> VideoLocals;
        private PocoIndex<int, SVR_VideoLocal_Place, int> ImportFolders;
        private PocoIndex<int, SVR_VideoLocal_Place, string> Paths;

        protected override int SelectKey(SVR_VideoLocal_Place entity)
        {
            return entity.VideoLocal_Place_ID;
        }

        public override void PopulateIndexes()
        {
            VideoLocals = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
            ImportFolders = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
            Paths = new PocoIndex<int, SVR_VideoLocal_Place, string>(Cache, a => a.FilePath);
        }

        public override void RegenerateDb()
        {
        }

        public List<SVR_VideoLocal_Place> GetByImportFolder(int importFolderID)
        {
            lock (Cache)
            {
                return ImportFolders.GetMultiple(importFolderID);
            }
        }

        public SVR_VideoLocal_Place GetByFilePathAndImportFolderID(string filePath, int nshareID)
        {
            lock (Cache)
            {
                return Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
            }
        }

        public List<SVR_VideoLocal_Place> GetByFilePathAndImportFolderType(string filePath, int folderType)
        {
            lock (Cache)
            {
                return Paths.GetMultiple(filePath).FindAll(a => a.ImportFolderType == folderType);
            }
        }

        public void DeleteWithoutChecking(SVR_VideoLocal_Place obj)
        {
            base.Delete(obj);
        }

        public override void Delete(SVR_VideoLocal_Place obj)
        {
            // Remove associated duplicate file records
            var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(obj.FilePath, obj.ImportFolderID);
            if (dups != null && dups.Count > 0) dups.ForEach(RepoFactory.DuplicateFile.Delete);

            base.Delete(obj);
        }

        public static Tuple<SVR_ImportFolder, string> GetFromFullPath(string fullPath)
        {
            IReadOnlyList<SVR_ImportFolder> shares = RepoFactory.ImportFolder.GetAll();

            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            foreach (SVR_ImportFolder ifolder in shares)
            {
                string importLocation = ifolder.ImportFolderLocation;
                string importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

                // add back the trailing back slashes
                importLocationFull = importLocationFull + $"{Path.DirectorySeparatorChar}";

                importLocation = importLocation.TrimEnd(Path.DirectorySeparatorChar);
                if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
                {
                    string filePath = fullPath.Replace(importLocation, string.Empty);
                    filePath = filePath.TrimStart(Path.DirectorySeparatorChar);
                    return new Tuple<SVR_ImportFolder, string>(ifolder, filePath);
                }
            }
            return null;
        }

        public List<SVR_VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            lock (Cache)
            {
                return VideoLocals.GetMultiple(videolocalid);
            }
        }
    }
}
