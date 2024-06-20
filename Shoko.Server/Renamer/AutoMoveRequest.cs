
#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents a request to automatically move a file.
/// </summary>
public record AutoMoveRequest : AutoRenameRequest
{

    /// <summary>
    /// Indicates whether empty directories should be deleted after
    /// relocating the file.
    /// </summary>
    public bool DeleteEmptyDirectories = true;

    /// <summary>
    /// Do the move operation.
    /// </summary>
    public bool Move = true;
}
