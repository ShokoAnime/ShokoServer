using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every provider's <see cref="RefreshMetadataJob{TProvider}"/> is
///   queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IMetadataRefreshJob
{
    /// <summary>
    ///   The AniDB anime whose linked entries are refreshed, or, with
    ///   <see cref="EntryID"/>, the anime the entry was refreshed for. 0 when
    ///   there is none.
    /// </summary>
    int AnimeID { get; set; }

    /// <summary>
    ///   One entry to refresh instead, as its metadata ID string.
    /// </summary>
    string? EntryID { get; set; }

    /// <summary>
    ///   Whether to refresh the entries however recently they were refreshed.
    /// </summary>
    bool Force { get; set; }

    /// <summary>
    ///   Whether to refresh the one entry asked for even when nothing links
    ///   to it.
    /// </summary>
    bool AllowUnlinked { get; set; }

    /// <summary>
    ///   Whether to queue the images of what was refreshed afterwards.
    /// </summary>
    bool DownloadImages { get; set; }

    /// <summary>
    ///   Whether to fetch the cast and crew, or <c>null</c> to go
    ///   by the settings.
    /// </summary>
    bool? DownloadCrewAndCast { get; set; }

    /// <summary>
    ///   Whether to fetch the alternate orderings, or <c>null</c>
    ///   to go by the settings.
    /// </summary>
    bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    ///   Whether to fetch the networks, or <c>null</c> to go by the
    ///   settings.
    /// </summary>
    bool? DownloadNetworks { get; set; }

    /// <summary>
    ///   Whether to fetch the collections, or <c>null</c> to go by
    ///   the settings.
    /// </summary>
    bool? DownloadCollections { get; set; }

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
        job.DownloadCrewAndCast = options.DownloadCrewAndCast;
        job.DownloadAlternateOrdering = options.DownloadAlternateOrdering;
        job.DownloadNetworks = options.DownloadNetworks;
        job.DownloadCollections = options.DownloadCollections;
        job.QuickRefresh = options.QuickRefresh;
        job.Reason = options.Reason;
    }

    /// <summary>
    ///   The options a refresh job was queued with, as the provider is told
    ///   them, without the refresh time and the anime.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The options.</returns>
    public static MetadataRefreshOptions ToOptions(this IMetadataRefreshJob job)
        => new()
        {
            DownloadImages = job.DownloadImages,
            DownloadCrewAndCast = job.DownloadCrewAndCast,
            DownloadAlternateOrdering = job.DownloadAlternateOrdering,
            DownloadNetworks = job.DownloadNetworks,
            DownloadCollections = job.DownloadCollections,
            QuickRefresh = job.QuickRefresh,
            Reason = job.Reason,
        };
}
