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
    ///   Indicates that <see cref="IsNetworkShare"/> has been explicitly set
    ///   (including to <c>null</c> to go back to detecting it).
    /// </summary>
    public bool HasIsNetworkShare { get; private set; }

    /// <summary>
    ///   Whether the managed folder is on a network share: <c>null</c> to
    ///   detect it, or <c>true</c> or <c>false</c> to say so. Only applied when
    ///   <see cref="HasIsNetworkShare"/> is <c>true</c>. See
    ///   <see cref="IManagedFolder.IsNetworkShare"/>.
    /// </summary>
    public bool? IsNetworkShare
    {
        get;
        set { HasIsNetworkShare = true; field = value; }
    }

    /// <summary>
    ///   The drop folder type. Set to <c>null</c> to leave unchanged.
    /// </summary>
    public DropFolderType? DropFolderType { get; set; }
}
