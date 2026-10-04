namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every provider's <see cref="DownloadMetadataImagesJob{TProvider}"/>
///   is queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IMetadataImagesJob
{
    /// <summary>
    ///   The series, film or collection whose images are linked, as its
    ///   metadata ID string.
    /// </summary>
    string EntryID { get; set; }

    /// <summary>
    ///   Whether to download the desired images again even when they are
    ///   there.
    /// </summary>
    bool Force { get; set; }

    /// <summary>
    ///   Whether this is the entry's first image run, after it was just linked
    ///   or created, which its contributors' jobs are ranked by.
    /// </summary>
    bool IsNew { get; set; }
}
