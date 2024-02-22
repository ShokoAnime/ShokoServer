using System;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko;

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

    public ImportFolder() { }

    public ImportFolder(SVR_ImportFolder folder)
    {
        var series = RepoFactory.VideoLocalPlace.GetByImportFolder(folder.ImportFolderID)
            .Select(a => a?.VideoLocal?.Hash).Where(a => !string.IsNullOrEmpty(a)).Distinct()
            .SelectMany(RepoFactory.CrossRef_File_Episode.GetByHash).DistinctBy(a => a.AnimeID).Count();
        var size = RepoFactory.VideoLocalPlace.GetByImportFolder(folder.ImportFolderID)
            .Select(a => a.VideoLocal).Where(b => b != null)
            .Sum(b => b.FileSize);

        DropFolderType type;
        if (folder.FolderIsDropDestination && folder.FolderIsDropSource)
        {
            type = DropFolderType.Both;
        }
        else if (folder.FolderIsDropDestination)
        {
            type = DropFolderType.Destination;
        }
        else if (folder.FolderIsDropSource)
        {
            type = DropFolderType.Source;
        }
        else
        {
            type = DropFolderType.None;
        }

        ID = folder.ImportFolderID;
        Name = folder.ImportFolderName;
        Path = folder.ImportFolderLocation;
        WatchForNewFiles = folder.FolderIsWatched;
        DropFolderType = type;
        Size = series;
        FileSize = size;
    }

    public SVR_ImportFolder GetServerModel()
    {
        return new SVR_ImportFolder
        {
            ImportFolderID = ID,
            ImportFolderName = Name,
            ImportFolderType = (int)ImportFolderType.HDD,
            ImportFolderLocation = Path,
            IsWatched = WatchForNewFiles ? 1 : 0,
            IsDropDestination = DropFolderType.HasFlag(DropFolderType.Destination) ? 1 : 0,
            IsDropSource = DropFolderType.HasFlag(DropFolderType.Source) ? 1 : 0
        };
    }
}

[Flags]
public enum DropFolderType
{
    None = 0,
    Source = 1,
    Destination = 2,
    Both = Source | Destination
}
