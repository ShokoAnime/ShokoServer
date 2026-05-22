using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Video;

/// <summary>
///   Data transfer object (DTO) for creating new managed folders.
/// </summary>
public sealed class ManagedFolderData
{
    /// <summary>
    ///   The friendly name of the managed folder.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///   The absolute path of the managed folder.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    ///   Whether to watch for new files in the managed folder.
    /// </summary>
    public bool WatchForNewFiles { get; set; } = false;

    /// <summary>
    ///   The drop folder type. Defaults to <see cref="DropFolderType.Excluded"/>.
    /// </summary>
    public DropFolderType DropFolderType { get; set; } = DropFolderType.Excluded;
}
