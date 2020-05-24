using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    public class ImportFolder : BaseModel
    {
        /// <summary>
        /// Import Folder ID
        /// </summary>
        public int ID { get; set; }
        
        /// <summary>
        /// Is the Folder watched by the filesystem watcher
        /// </summary>
        /// <returns></returns>
        public bool WatchForNewFiles { get; set; }
        
        /// <summary>
        /// Whether the import folder is a drop folder
        /// </summary>
        public DropFolderType DropFolderType { get; set; }
        
        /// <summary>
        /// Path on the server where the import folder exists. For docker, it's inside the container, so it'll look excessively simple
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// Total FileSize of the contents of the ImportFolder
        /// </summary>
        public long FileSize { get; set; }
        
        // TODO Maybe add cloud stuff. It's nicer to add later than to take away
        
        public ImportFolder() {}

        public ImportFolder(SVR_ImportFolder folder)
        {
            var series = RepoFactory.VideoLocalPlace.GetByImportFolder(folder.ImportFolderID)
                .DistinctBy(b => b.VideoLocalID)
                .SelectMany(b =>
                    string.IsNullOrEmpty(b.VideoLocal?.Hash)
                        ? null
                        : RepoFactory.CrossRef_File_Episode.GetByHash(b.VideoLocal.Hash)).Where(b => b != null)
                .DistinctBy(b => b.AnimeID).Count();
            long size = RepoFactory.VideoLocalPlace.GetByImportFolder(folder.ImportFolderID)
                .DistinctBy(b => b.VideoLocalID).Select(b => b.VideoLocal).Where(b => b != null)
                .Sum(b => b.FileSize);

            DropFolderType type;
            if (folder.FolderIsDropDestination) type = DropFolderType.Destination;
            else if (folder.FolderIsDropSource) type = DropFolderType.Source;
            else type = DropFolderType.None;

            ID = folder.ImportFolderID;
            Name = folder.ImportFolderName;
            Path = folder.ImportFolderLocation;
            WatchForNewFiles = folder.FolderIsWatched;
            DropFolderType = type;
            Size = series;
            FileSize = size;
        }

        public Shoko.Models.Server.ImportFolder GetServerModel()
        {
            return new Shoko.Models.Server.ImportFolder
            {
                ImportFolderID = ID,
                ImportFolderName = Name,
                ImportFolderType = (int) ImportFolderType.HDD,
                ImportFolderLocation = Path,
                IsWatched = WatchForNewFiles ? 1 : 0,
                IsDropDestination = DropFolderType == DropFolderType.Destination ? 1 : 0,
                IsDropSource = DropFolderType == DropFolderType.Source ? 1 : 0
            };
        }
    }
    public enum DropFolderType
    {
        None = 0,
        Source = 1,
        Destination = 2
    }
}