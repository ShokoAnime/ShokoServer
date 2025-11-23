using System.Threading;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
///   Represents a request to automatically relocate a file to it's most optimal
///   location, using the provided or default relocation pipe.
/// </summary>
public record AutoRelocateRequest
{
    /// <summary>
    ///   The cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    ///   The relocation pipe to use. If not provided, the default pipe will be
    ///   used.
    /// </summary>
    public IRelocationPipe? Pipe { get; set; } = null;

    /// <summary>
    ///   Indicates whether the result should be a preview of the
    ///   relocation. So determine the location without actually moving the
    ///   file.
    /// </summary>
    public bool Preview { get; set; }

    /// <summary>
    ///   Do the rename operation.
    /// </summary>
    public bool Rename { get; set; } = true;

    /// <summary>
    ///   Do the move operation.
    /// </summary>
    public bool Move { get; set; } = true;

    /// <summary>
    ///   Indicates whether empty directories should be deleted after relocating
    ///   the file.
    /// </summary>
    public bool DeleteEmptyDirectories { get; set; } = true;

    /// <summary>
    ///   Indicates that we can relocate a video file that lives inside a
    ///   managed folder marked as a destination that's not also marked as a
    ///   source.
    /// </summary>
    public bool AllowRelocationInsideDestination { get; set; } = true;

    /// <summary>
    ///   Indicates whether the new operation should be cancelled if an existing
    ///   operation is already running for the same file.
    /// </summary>
    public bool CancelIfRunning { get; set; } = false;
}
