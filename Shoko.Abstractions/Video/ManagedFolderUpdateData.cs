using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Video;

/// <summary>
///   Data transfer object (DTO) for updating existing managed folders.
///   Supports partial updates — only non-null fields are applied.
/// </summary>
public sealed class ManagedFolderUpdateData
{
    /// <summary>
    ///   The new name of the managed folder. Set to <c>null</c> to leave
    ///   unchanged.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   The new path of the managed folder. Set to <c>null</c> to leave
    ///   unchanged.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    ///   Whether to watch for new files. Set to <c>null</c> to leave unchanged.
    /// </summary>
    public bool? WatchForNewFiles { get; set; }

    /// <summary>
    ///   The drop folder type. Set to <c>null</c> to leave unchanged.
    /// </summary>
    public DropFolderType? DropFolderType { get; set; }
}
