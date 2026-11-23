using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Abstractions.FileSystem.Services;
using Shoko.Abstractions.Video;

namespace Shoko.Server.Services;

/// <summary>
///   Tells, for one run of work that removes the records of missing files,
///   whether a managed folder's storage is still there, so an unmounted share
///   or a dead network never reads as every file being gone.
/// </summary>
/// <remarks>
///   A file that is not found looks the same whether it was deleted or its
///   volume went away, so this looks at the folder's root instead. The first
///   time a folder is asked about, its root must be a readable, non-empty
///   directory (an unmounted mount point is usually an empty local folder),
///   and its volume is remembered. Afterwards the root must still be on that
///   volume: an unmount mid-run puts the mount point on its parent's volume.
/// </remarks>
/// <param name="fileSystem">The file system.</param>
internal sealed class ManagedFolderPresence(IFileSystem fileSystem)
{
    private readonly Dictionary<int, string?> _volumes = [];

    /// <summary>
    ///   Checks whether the folder's storage is there now.
    /// </summary>
    /// <param name="folder">The managed folder.</param>
    /// <returns><c>true</c> when its records may be removed for missing files.</returns>
    public bool IsPresent(IManagedFolder folder)
    {
        if (!_volumes.TryGetValue(folder.ID, out var expected))
        {
            expected = ReadVolume(folder.Path, requireEntries: true);
            _volumes[folder.ID] = expected;
            return expected is not null;
        }

        return expected is not null && ReadVolume(folder.Path, requireEntries: false) == expected;
    }

    private string? ReadVolume(string path, bool requireEntries)
    {
        try
        {
            if (fileSystem.GetDirectoryStat(path) is not { } directory)
                return null;

            if (requireEntries && fileSystem.IsDirectoryEmpty(path))
                return null;

            return directory.Identity.VolumeID;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
