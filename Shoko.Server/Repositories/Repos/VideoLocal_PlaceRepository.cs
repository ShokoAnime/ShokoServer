using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class VideoLocal_PlaceRepository : BaseRepository<SVR_VideoLocal_Place, int>
    {
        private PocoIndex<int, SVR_VideoLocal_Place, int> VideoLocals;
        private PocoIndex<int, SVR_VideoLocal_Place, int> ImportFolders;
        private PocoIndex<int, SVR_VideoLocal_Place, string> Paths;



        internal override int SelectKey(SVR_VideoLocal_Place entity) => entity.VideoLocal_Place_ID;

        internal override void PopulateIndexes()
        {
            VideoLocals = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
            ImportFolders = new PocoIndex<int, SVR_VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
            Paths = new PocoIndex<int, SVR_VideoLocal_Place, string>(Cache, a => a.FilePath);
        }

        internal override void ClearIndexes()
        {
            VideoLocals = null;
            ImportFolders = null;
            Paths = null;
        }


        public List<SVR_VideoLocal_Place> GetByImportFolder(int importFolderID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ImportFolders.GetMultiple(importFolderID);
                return Table.Where(a => a.ImportFolderID == importFolderID).ToList();
            }
        }

        public SVR_VideoLocal_Place GetByFilePathAndShareID(string filePath, int nshareID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
                return Table.FirstOrDefault(a => a.FilePath == filePath && a.ImportFolderID == nshareID);
            }
        }

        public List<SVR_VideoLocal_Place> GetByFilePathAndImportFolderType(string filePath, int folderType)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Paths.GetMultiple(filePath).FindAll(a => a.ImportFolderType == folderType);
                return Table.Where(a => a.FilePath == filePath && a.ImportFolderType == folderType).ToList();
            }
        }
        public List<SVR_VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return VideoLocals.GetMultiple(videolocalid);
                return Table.Where(a => a.VideoLocalID == videolocalid).ToList();
            }
        }

        internal override object BeginDelete(SVR_VideoLocal_Place entity, object parameters)
        {
            var dups = Repo.DuplicateFile.GetByFilePathAndImportFolder(entity.FilePath, entity.ImportFolderID);
            if (dups != null && dups.Count > 0)
                Repo.DuplicateFile.Delete(dups);
            return null;
        }

        internal override void EndDelete(SVR_VideoLocal_Place entity, object returnFromBeginDelete, object parameters)
        {
            Repo.AnimeEpisode.Touch(() => entity.VideoLocal.GetAnimeEpisodes());
        }


        public static (SVR_ImportFolder, string) GetFromFullPath(string fullPath)
        {
            IReadOnlyList<SVR_ImportFolder> shares = Repo.ImportFolder.GetAll();

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
                    return (ifolder, filePath);
                }
            }
            return (null,null);
        }
        public List<SVR_VideoLocal_Place> GetByFilename(string name)
        {
            using (RepoLock.ReaderLock())
            {
                return Where(v => name.Equals(v.FilePath.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        internal SVR_VideoLocal_Place GetByFilePathAndImportFolderID(string filePath, int nshareID)
        {
            using (RepoLock.ReaderLock())
            {
                return Where(a => a.FilePath == filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
            }
        }
    }
}