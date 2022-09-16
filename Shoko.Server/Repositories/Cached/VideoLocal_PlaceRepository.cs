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

        public VideoLocal_PlaceRepository()
        {
            BeginDeleteCallback = place =>
            {
                // Remove associated duplicate file records
                var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(place.FilePath, place.ImportFolderID);
                if (dups is { Count: > 0 }) dups.ForEach(RepoFactory.DuplicateFile.Delete);
            };
        }

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
            return ReadLock(() => ImportFolders.GetMultiple(importFolderID));
        }

        public SVR_VideoLocal_Place GetByFilePathAndImportFolderID(string filePath, int nshareID)
        {
            return ReadLock(() => Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID));
        }

        public static Tuple<SVR_ImportFolder, string> GetFromFullPath(string fullPath)
        {
            var shares = RepoFactory.ImportFolder.GetAll();

            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            foreach (var ifolder in shares)
            {
                var importLocation = ifolder.ImportFolderLocation;
                var importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

                // add back the trailing back slashes
                importLocationFull += $"{Path.DirectorySeparatorChar}";

                importLocation = importLocation.TrimEnd(Path.DirectorySeparatorChar);
                if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
                {
                    var filePath = fullPath.Replace(importLocation, string.Empty);
                    filePath = filePath.TrimStart(Path.DirectorySeparatorChar);
                    return new Tuple<SVR_ImportFolder, string>(ifolder, filePath);
                }
            }
            return null;
        }

        public List<SVR_VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            return ReadLock(() => VideoLocals.GetMultiple(videolocalid));
        }
    }
}
