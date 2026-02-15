using System.Threading;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Relocation;

/// <summary>
///   Represents a request to directly relocate a file.
/// </summary>
public record DirectlyRelocateRequest
{
    /// <summary>
    ///   The cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    ///   The managed folder where the file should be relocated to.
    /// </summary>
    public IManagedFolder? ManagedFolder { get; set; } = null;

    /// <summary>
    ///   The relative path from the <see cref="ManagedFolder"/> where the file
    ///   should be relocated to.
    /// </summary>
    public string? RelativePath { get; set; } = null;

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
