using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models.Server;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocal_PlaceRepository : BaseCachedRepository<SVR_VideoLocal_Place, int>
    {
        private PocoIndex<int, SVR_VideoLocal_Place, int> VideoLocals;
        private PocoIndex<int, SVR_VideoLocal_Place, int> ImportFolders;
        private PocoIndex<int, SVR_VideoLocal_Place, string> Paths;

        private VideoLocal_PlaceRepository()
        {
        }

        public static VideoLocal_PlaceRepository Create()
        {
            return new VideoLocal_PlaceRepository();
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
            return ImportFolders.GetMultiple(importFolderID);
        }

        public SVR_VideoLocal_Place GetByFilePathAndShareID(string filePath, int nshareID)
        {
            return Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
        }

        public List<SVR_VideoLocal_Place> GetByFilePathAndImportFolderType(string filePath, int folderType)
        {
            return Paths.GetMultiple(filePath).FindAll(a => a.ImportFolderType == folderType);
        }

        public override void Delete(SVR_VideoLocal_Place obj)
        {
            // Remove associated duplicate file records
            var dups = RepoFactory.DuplicateFile.GetByFilePathAndImportFolder(obj.FilePath, obj.ImportFolderID);
            if (dups != null && dups.Count > 0) dups.ForEach(RepoFactory.DuplicateFile.Delete);

            base.Delete(obj);
            foreach (SVR_AnimeEpisode ep in obj.VideoLocal.GetAnimeEpisodes())
            {
                RepoFactory.AnimeEpisode.Save(ep);
            }
        }

        public static Tuple<SVR_ImportFolder, string> GetFromFullPath(string fullPath)
        {
            IReadOnlyList<SVR_ImportFolder> shares = RepoFactory.ImportFolder.GetAll();

            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            foreach (SVR_ImportFolder ifolder in shares)
            {
                string importLocation = ifolder.ImportFolderLocation;
                string importLocationFull = importLocation.TrimEnd(System.IO.Path.DirectorySeparatorChar);

                // add back the trailing back slashes
                importLocationFull = importLocationFull + $"{System.IO.Path.DirectorySeparatorChar}";

                importLocation = importLocation.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
                {
                    string filePath = fullPath.Replace(importLocation, string.Empty);
                    filePath = filePath.TrimStart(System.IO.Path.DirectorySeparatorChar);
                    return new Tuple<SVR_ImportFolder, string>(ifolder, filePath);
                }
            }
            return null;
        }

        public List<SVR_VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            return VideoLocals.GetMultiple(videolocalid);
        }
    }
}