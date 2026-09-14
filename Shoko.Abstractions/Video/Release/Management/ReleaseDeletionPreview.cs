using System.Collections.Generic;

namespace Shoko.Abstractions.Video.Release.Management;

/// <summary>
/// Preview of the files that would be deleted for a single series.
/// </summary>
public class ReleaseDeletionPreview
{
    /// <summary>
    /// Shoko series ID.
    /// </summary>
    public required int SeriesID { get; init; }

    /// <summary>
    /// Display title for the series.
    /// </summary>
    public required string SeriesTitle { get; init; }

    /// <summary>
    /// AniDB anime ID.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    /// Number of files that would be deleted.
    /// </summary>
    public required int TotalFilesToDelete { get; init; }

    /// <summary>
    /// Total size in bytes of files that would be deleted.
    /// </summary>
    public required long TotalSizeToDelete { get; init; }

    /// <summary>
    /// File locations that would be deleted for this series. Use the
    /// <see cref="ReleaseDeletionPreviewFile.PlaceID"/> values as input to
    /// <see cref="Services.IReleaseManagementService.QueueDeletion"/>.
    /// </summary>
    public required IReadOnlyList<ReleaseDeletionPreviewFile> Files { get; init; }
}

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
