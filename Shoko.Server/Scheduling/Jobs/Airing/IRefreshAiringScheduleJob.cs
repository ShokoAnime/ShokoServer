using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// What every provider's <see cref="RefreshAiringScheduleJob{TProvider}"/> is
/// queued with, for a caller that knows the job type only at runtime.
/// </summary>
internal interface IRefreshAiringScheduleJob
{
    /// <summary>
    /// The source of the entity to refresh.
    /// </summary>
    MetadataSource EntitySource { get; set; }

    /// <summary>
    /// The kind of entity to refresh.
    /// </summary>
    MetadataEntityType EntityType { get; set; }

    /// <summary>
    /// The ID of the entity within its source.
    /// </summary>
    string EntityID { get; set; }
}
