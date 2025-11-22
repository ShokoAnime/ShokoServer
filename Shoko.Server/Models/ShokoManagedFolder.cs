using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models;

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
        get => _path;
        set => _path = Utils.CleanPath(value, osDependent: true) + System.IO.Path.DirectorySeparatorChar;
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

    /// <summary>
    /// Helper to get all accessible files in the folder recursively from the
    /// file system.
    /// </summary>
    public IReadOnlyList<string> Files
    {
        get
        {
            if (!Directory.Exists(Path))
                return [];
            var exclusions = Utils.SettingsProvider.GetSettings().Import.Exclude
                .Select(s => new Regex(s, RegexOptions.Compiled))
                .ToArray();
            bool IsMatch(string p) => !exclusions.Any(r => r.IsMatch(p));
            return FileSystemHelpers.GetFilePaths(Path, recursive: true, filter: IsMatch);
        }
    }

    public override string ToString()
    {
        return string.Format("{0} - {1} ({2})", Name, Path, ID);
    }

    #region IManagedFolder Implementation

    bool IManagedFolder.WatchForNewFiles => IsWatched;

    #endregion
}
