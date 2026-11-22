using Shoko.Abstractions.FileSystem;

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
    /// <param name="entry">
    ///   The file or folder: its path and whether it is a directory are known
    ///   at once, and its stat is read only if the rule asks for it, so a rule
    ///   looking only at the path costs a scan nothing.
    /// </param>
    /// <returns>
    ///   True if the file or folder should be ignored.
    /// </returns>
    bool ShouldIgnore(IManagedFolder folder, FileSystemEntry entry);
}
