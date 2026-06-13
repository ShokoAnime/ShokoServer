using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Release;

/// <summary>
/// Preview of files that would be deleted for a single series.
/// </summary>
public class ReleaseDeletionPreview
{
    /// <summary>Shoko series ID.</summary>
    [Required]
    public required int SeriesID { get; init; }

    /// <summary>Display title for the series.</summary>
    [Required]
    public required string SeriesTitle { get; init; }

    /// <summary>AniDB anime ID.</summary>
    [Required]
    public required int AnidbAnimeID { get; init; }

    /// <summary>Number of files that would be deleted.</summary>
    [Required]
    public required int TotalFilesToDelete { get; init; }

    /// <summary>Total size in bytes of files that would be deleted.</summary>
    [Required]
    public required long TotalSizeToDelete { get; init; }

    /// <summary>
    /// File locations that would be deleted for this series.
    /// Use the <see cref="FileLocation.PlaceID"/> values as input
    /// to the execute endpoint.
    /// </summary>
    [Required]
    public required IReadOnlyList<FileLocation> Files { get; init; }

    /// <summary>
    /// A file location that would be deleted.
    /// </summary>
    public class FileLocation
    {
        /// <summary>VideoLocal_Place ID — use this in the execute body.</summary>
        [Required]
        public required int PlaceID { get; init; }

        /// <summary>VideoLocal ID.</summary>
        [Required]
        public required int VideoLocalID { get; init; }

        /// <summary>
        /// Absolute file path, or null if the managed folder is unavailable.
        /// </summary>
        public string? AbsolutePath { get; init; }

        /// <summary>File size in bytes.</summary>
        [Required]
        public required long FileSize { get; init; }
    }
}
