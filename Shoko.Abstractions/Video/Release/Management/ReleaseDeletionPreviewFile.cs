namespace Shoko.Abstractions.Video.Release.Management;

/// <summary>
/// A single file location that would be deleted.
/// </summary>
public class ReleaseDeletionPreviewFile
{
    /// <summary>
    /// The file location ID — use this when queueing the deletion.
    /// </summary>
    public required int PlaceID { get; init; }

    /// <summary>
    /// The video ID.
    /// </summary>
    public required int VideoID { get; init; }

    /// <summary>
    /// Absolute file path, or null if the managed folder is unavailable.
    /// </summary>
    public string? AbsolutePath { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long FileSize { get; init; }
}
