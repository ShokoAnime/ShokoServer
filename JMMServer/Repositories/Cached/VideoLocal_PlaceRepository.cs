using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class VideoLocal_PlaceRepository : BaseCachedRepository<VideoLocal_Place,int>
    {
        private PocoIndex<int, VideoLocal_Place, int> VideoLocals;
        private PocoIndex<int, VideoLocal_Place, int> ImportFolders;
        private PocoIndex<int, VideoLocal_Place, string> Paths;

        private VideoLocal_PlaceRepository()
        {
            
        }

        public static VideoLocal_PlaceRepository Create()
        {
            return new VideoLocal_PlaceRepository();
        }
        public override void PopulateIndexes()
        {
            VideoLocals = new PocoIndex<int, VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
            ImportFolders = new PocoIndex<int, VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
            Paths = new PocoIndex<int, VideoLocal_Place, string>(Cache, a => a.FilePath);
        }

        public override void RegenerateDb()
        {
        }

        public List<VideoLocal_Place> GetByImportFolder(int importFolderID)
        {
            return ImportFolders.GetMultiple(importFolderID);
        }
        public VideoLocal_Place GetByFilePathAndShareID(string filePath, int nshareID)
        {
            return Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
        }



        public static Tuple<ImportFolder, string> GetFromFullPath(string fullPath)
        {
            List<ImportFolder> shares = RepoFactory.ImportFolder.GetAll();

            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            foreach (ImportFolder ifolder in shares)
            {
                string importLocation = ifolder.ParsedImportFolderLocation;
                string importLocationFull = importLocation.TrimEnd('\\');

                // add back the trailing back slashes
                importLocationFull = importLocationFull + "\\";

                importLocation = importLocation.TrimEnd('\\');
                if (fullPath.StartsWith(importLocationFull))
                {
                    string filePath = fullPath.Replace(importLocation, string.Empty);
                    filePath = filePath.TrimStart('\\');
                    return new Tuple<ImportFolder, string>(ifolder,filePath);
                }
            }
            return null;
        }

        public List<VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            return VideoLocals.GetMultiple(videolocalid);
        }

    }
}
