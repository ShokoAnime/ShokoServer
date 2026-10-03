namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every provider's <see cref="RefreshMetadataEntityJob{TProvider}"/>
///   is queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IMetadataEntityRefreshJob
{
    /// <summary>
    ///   The creator, character, studio or network to refresh, as its
    ///   metadata ID string.
    /// </summary>
    string EntityID { get; set; }

    /// <summary>
    ///   Whether to refresh it however fresh it is.
    /// </summary>
    bool Force { get; set; }
}
