using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Extensions;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class ManagedFolder : BaseModel
{
    /// <summary>
    ///   Managed Folder ID.
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    ///   Determines if the filesystem watcher to watch for new files on the
    ///   managed folder is enabled.
    /// </summary>
    public bool WatchForNewFiles { get; set; }

    /// <summary>
    ///   Indicates if the managed folder is a drop destination, a drop source,
    ///   both, or neither.
    /// </summary>
    public DropFolderType DropFolderType { get; set; }

    /// <summary>
    ///   Path on the server where the managed folder exists.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///   Total FileSize of the contents of the managed folder.
    /// </summary>
    public long FileSize { get; set; }

    public ManagedFolder() { }

    public ManagedFolder(ShokoManagedFolder folder)
    {
        var places = folder.Places;
        var series = places
            .Select(a => a?.VideoLocal?.Hash)
            .WhereNotNullOrDefault()
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
        => new()
        {
            ID = ID,
            Name = Name,
            Path = Path,
            IsWatched = WatchForNewFiles,
            IsDropDestination = DropFolderType.HasFlag(DropFolderType.Destination),
            IsDropSource = DropFolderType.HasFlag(DropFolderType.Source),
        };

    public static class Input
    {
        public class CreateManagedFolderBody
        {
            /// <summary>
            ///   The friendly name of the managed folder.
            /// </summary>
            [Required]
            public string Name { get; set; } = string.Empty;

            /// <summary>
            ///   Path on the server where the managed folder exists.
            /// </summary>
            [Required]
            public string Path { get; set; } = string.Empty;

            /// <summary>
            ///   Enable the filesystem watcher to watch for new files on the
            ///   managed folder.
            /// </summary>
            public bool WatchForNewFiles { get; set; } = false;

            /// <summary>
            ///   Determines whether the managed folder is a drop destination, a
            ///   drop source, both, or neither.
            /// </summary>
            public DropFolderType DropFolderType { get; set; } = DropFolderType.None;

            public ShokoManagedFolder GetServerModel()
                => new()
                {
                    Name = Name,
                    Path = Path,
                    IsWatched = WatchForNewFiles,
                    IsDropDestination = DropFolderType.HasFlag(DropFolderType.Destination),
                    IsDropSource = DropFolderType.HasFlag(DropFolderType.Source),
                };
        }

        /// <summary>
        ///   Used to notify the server that something may have been changes at
        ///   the given absolute path.
        /// </summary>
        public class NotifyChangeDetectedAbsoluteBody
        {
            /// <summary>
            ///   The absolute path to check.
            /// </summary>
            [Required]
            public string Path { get; set; } = string.Empty;

            /// <summary>
            ///   Optional. Set to <c>false</c> to not add or remove the release
            ///   to the user's MyList if any releases are found and saved for
            ///   any video files or if any video files have been deleted.
            /// </summary>
            public bool UpdateMyList { get; set; } = true;
        }

        /// <summary>
        ///   Used to notify the server that something may have been changes at
        ///   the given path relative to the managed folder.
        /// </summary>
        public class NotifyChangeDetectedRelativeBody
        {
            /// <summary>
            ///   The relative path to check.
            /// </summary>
            public string? RelativePath { get; set; }

            /// <summary>
            ///   Optional. Set to <c>false</c> to not add or remove the release
            ///   to the user's MyList if any releases are found and saved for
            ///   any video files or if any video files have been deleted.
            /// </summary>
            public bool UpdateMyList { get; set; } = true;
        }
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
