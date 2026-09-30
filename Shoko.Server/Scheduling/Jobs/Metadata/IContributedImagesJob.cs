namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every contributor's <see cref="DownloadContributedImagesJob{TContributor}"/>
///   is queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IContributedImagesJob
{
    /// <summary>
    ///   The series, film or collection whose entities get the images, as its
    ///   metadata ID string.
    /// </summary>
    string EntryID { get; set; }

    /// <summary>
    ///   Whether to download the desired images again even when they are
    ///   there.
    /// </summary>
    bool Force { get; set; }
}
