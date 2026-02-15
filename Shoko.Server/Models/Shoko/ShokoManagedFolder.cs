using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

/// <summary>
/// A folder managed by Shoko.
/// </summary>
public class ShokoManagedFolder : IManagedFolder
{
    /// <inheritdoc/>
    public int ID { get; set; }

    /// <inheritdoc/>
    public string Name { get; set; } = string.Empty;

    private string _path = string.Empty;

    /// <inheritdoc/>
    public string Path
    {
        // Normalize the path to the current platform.
        get => PlatformUtility.NormalizePath(_path, platformFormat: true) + System.IO.Path.DirectorySeparatorChar;
        // Normalize the path to the internal format.
        set => _path = System.IO.Path.IsPathFullyQualified(value) || System.IO.Path.IsPathRooted(value)
            ? PlatformUtility.NormalizePath(value)
            : throw new ArgumentException("The path must be fully qualified.");
    }

    /// <summary>
    /// Indicates that file watching should be enabled for the folder.
    /// </summary>
    public bool IsWatched { get; set; }

    /// <summary>
    /// Indicates that the folder is a drop source in the rename/move system.
    /// </summary>
    public bool IsDropSource { get; set; }

    /// <summary>
    /// Indicates that the folder is a drop destination in the rename/move system.
    /// </summary>
    public bool IsDropDestination { get; set; }

    /// <inheritdoc/>
    public long AvailableFreeSpace
    {
        get
        {
            var path = Path;
            if (!Directory.Exists(path))
                return -1L;

            try
            {
                return new DriveInfo(path).AvailableFreeSpace;
            }
            catch
            {
                return -2L;
            }
        }
    }

    /// <inheritdoc/>
    public DropFolderType DropFolderType
    {
        get
        {
            return true switch
            {
                _ when IsDropSource && IsDropDestination => DropFolderType.Both,
                _ when IsDropSource => DropFolderType.Source,
                _ when IsDropDestination => DropFolderType.Destination,
                _ => DropFolderType.Excluded,
            };
        }
        set
        {
            IsDropSource = value.HasFlag(DropFolderType.Source);
            IsDropDestination = value.HasFlag(DropFolderType.Destination);
        }
    }

    /// <summary>
    /// Helper to get all video file locations stored in the database for the folder.
    /// </summary>
    public IReadOnlyList<VideoLocal_Place> Places
        => RepoFactory.VideoLocalPlace.GetByManagedFolderID(ID);

    public override string ToString()
    {
        return string.Format("{0} - {1} ({2})", Name, Path, ID);
    }

    #region IManagedFolder Implementation

    bool IManagedFolder.WatchForNewFiles => IsWatched;

    #endregion
}
