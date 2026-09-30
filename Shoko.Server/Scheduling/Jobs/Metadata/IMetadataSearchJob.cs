namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   What every provider's <see cref="SearchMetadataJob{TProvider}"/> is
///   queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IMetadataSearchJob
{
    /// <summary>
    ///   The AniDB anime to work out.
    /// </summary>
    int AnimeID { get; set; }

    /// <summary>
    ///   Whether the search ignores the auto-link setting and the anime's veto.
    /// </summary>
    bool Force { get; set; }

    /// <summary>
    ///   Whether a person asked for this one anime, which also searches an
    ///   anime already linked and replaces its links with what is taken.
    /// </summary>
    bool Replace { get; set; }
}
