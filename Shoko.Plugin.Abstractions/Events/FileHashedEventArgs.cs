using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Hashing;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a file is hashed and ready for use.
/// </summary>
/// <param name="relativePath">Relative path to the file.</param>
/// <param name="managedFolder">The managed folder that the file is in.</param>
/// <param name="fileInfo">The <see cref="IVideoFile"/> information for the file.</param>
/// <param name="videoInfo">The <see cref="IVideo"/> information for the file.</param>
public class FileHashedEventArgs(string relativePath, IManagedFolder managedFolder, IVideoFile fileInfo, IVideo videoInfo) : FileEventArgs(relativePath, managedFolder, fileInfo, videoInfo)
{
    /// <summary>
    /// Indicates that the hashes may have been reused from an existing video.
    /// </summary>
    public required bool UsedExistingHashes { get; init; }

    /// <summary>
    /// Indicates that the video was just added to the database as a result of
    /// this operation.
    /// </summary>
    public required bool IsNewVideo { get; init; }

    /// <summary>
    /// Indicates that the file was just added to the database as a result of
    /// this operation.
    /// </summary>
    public required bool IsNewFile { get; init; }

    /// <summary>
    /// The hashes that were the result of the operation. May or may not have
    /// been reused depending on the provider(s) enabled and if it was requested
    /// to re-use existing hashes.
    /// </summary>
    public required IReadOnlyList<IHashDigest> Hashes { get; init; }
}
