
using System.IO;

namespace Shoko.Abstractions.Video;

/// <summary>
///   Interface for rules that determine if a video file should be ignored by Shoko.
/// </summary>
public interface IManagedFolderIgnoreRule
{
    /// <summary>
    ///   The friendly name of the rule.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   Determines if a file or folder inside a managed folder should be
    ///   ignored by Shoko.
    /// </summary>
    /// <param name="folder">
    ///   The managed folder we're checking in.
    /// </param>
    /// <param name="fileSystemInfo">
    ///   The potential video file or folder which may potentially potentially
    ///   contain video files.
    /// </param>
    /// <returns>
    ///   True if the file or folder should be ignored.
    /// </returns>
    bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileSystemInfo);
}
