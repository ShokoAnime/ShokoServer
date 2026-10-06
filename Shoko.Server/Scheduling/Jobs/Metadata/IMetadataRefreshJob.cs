using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every provider's <see cref="RefreshMetadataJob{TProvider}"/> is
///   queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IMetadataRefreshJob
{
    /// <summary>
    ///   The series, film or collection to refresh, as its metadata ID string.
    /// </summary>
    string EntryID { get; set; }

    /// <summary>
    ///   Whether to refresh the entry however recently it was refreshed.
    /// </summary>
    bool Force { get; set; }

    /// <summary>
    ///   Whether to refresh the entry even when nothing links to it.
    /// </summary>
    bool AllowUnlinked { get; set; }

    /// <summary>
    ///   Whether to queue the images of what was refreshed afterwards.
    /// </summary>
    bool DownloadImages { get; set; }

    /// <summary>
    ///   Whether to fetch the alternate orderings, or <c>null</c>
    ///   to go by the settings.
    /// </summary>
    bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    ///   Whether this is a quick refresh, which leaves the images alone.
    /// </summary>
    bool QuickRefresh { get; set; }

    /// <summary>
    ///   Why the refresh was queued.
    /// </summary>
    MetadataRefreshReason Reason { get; set; }
}

/// <summary>
///   Reads and writes the refresh options a refresh job is queued with.
/// </summary>
internal static class MetadataRefreshJobExtensions
{
    /// <summary>
    ///   Copies the options the provider is told onto a refresh job.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="options">The options.</param>
    public static void Apply(this IMetadataRefreshJob job, MetadataRefreshOptions options)
    {
        job.DownloadImages = options.DownloadImages;
        job.DownloadAlternateOrdering = options.DownloadAlternateOrdering;
        job.QuickRefresh = options.QuickRefresh;
        job.Reason = options.Reason;
    }

    /// <summary>
    ///   The options a refresh job was queued with, as the provider is told
    ///   them, without the refresh time.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The options.</returns>
    public static MetadataRefreshOptions ToOptions(this IMetadataRefreshJob job)
        => new()
        {
            DownloadImages = job.DownloadImages,
            DownloadAlternateOrdering = job.DownloadAlternateOrdering,
            QuickRefresh = job.QuickRefresh,
            Reason = job.Reason,
        };
}
