using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko;

public class ManagedFolder : BaseModel
{
    /// <summary>
    /// Managed Folder ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// Is the Folder watched by the filesystem watcher
    /// </summary>
    /// <returns></returns>
    public bool WatchForNewFiles { get; set; }

    /// <summary>
    /// Whether the managed folder is a drop folder
    /// </summary>
    public DropFolderType DropFolderType { get; set; }

    /// <summary>
    /// Path on the server where the managed folder exists. For docker, it's inside the container, so it'll look excessively simple
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Total FileSize of the contents of the managed folder
    /// </summary>
    public long FileSize { get; set; }

    public ManagedFolder() { }

    public ManagedFolder(ShokoManagedFolder folder)
    {
        var places = folder.Places;
        var series = places
            .Select(a => a?.VideoLocal?.Hash)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .SelectMany(RepoFactory.CrossRef_File_Episode.GetByEd2k)
            .DistinctBy(a => a.AnimeID)
            .Count();
        var size = places
            .Select(a => a.VideoLocal)
            .WhereNotNull()
            .Sum(b => b.FileSize);

        var type = DropFolderType.None;
        if (folder.IsDropDestination && folder.IsDropSource)
            type = DropFolderType.Both;
        else if (folder.IsDropDestination)
            type = DropFolderType.Destination;
        else if (folder.IsDropSource)
            type = DropFolderType.Source;

        ID = folder.ID;
        Name = folder.Name;
        Path = folder.Path;
        WatchForNewFiles = folder.IsWatched;
        DropFolderType = type;
        Size = series;
        FileSize = size;
    }

    public ShokoManagedFolder GetServerModel()
    {
        return new ShokoManagedFolder
        {
            ID = ID,
            Name = Name,
            Path = Path,
            IsWatched = WatchForNewFiles,
            IsDropDestination = DropFolderType.HasFlag(DropFolderType.Destination),
            IsDropSource = DropFolderType.HasFlag(DropFolderType.Source),
        };
    }
}

[Flags]
[JsonConverter(typeof(StringEnumConverter))]
public enum DropFolderType
{
    None = 0,
    Source = 1,
    Destination = 2,
    Both = Source | Destination,
}
