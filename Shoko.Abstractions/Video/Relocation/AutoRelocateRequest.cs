using System.Threading;

namespace Shoko.Abstractions.Video.Relocation;

/// <summary>
///   Represents a request to automatically relocate a file to it's most optimal
///   location, using the provided or default relocation preset.
/// </summary>
public record AutoRelocateRequest
{
    /// <summary>
    ///   The cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    ///   The relocation preset to use. If not provided, the default preset will
    ///   be used.
    /// </summary>
    public IRelocationPreset? Preset { get; set; } = null;

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
    ///   Indicates that relocating onto the path of another copy of the same
    ///   video replaces it rather than leaving the file where it is. A link to
    ///   the file, or a hard link of it, is removed instead of moved, and a
    ///   link at the destination is replaced by the file.
    /// </summary>
    public bool ReplaceSameVideoAtDestination { get; set; }

    /// <summary>
    ///   Not read. A relocation already running for the same file neither
    ///   queues nor cancels a new one, which starts at once, alongside it.
    /// </summary>
    public bool CancelIfRunning { get; set; } = false;
}
