using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
/// Dispatches supplementary metadata work to all registered
/// <see cref="ISupplementaryMetadataProvider"/> implementations.
/// </summary>
public interface ISupplementaryMetadataService
{
    /// <summary>
    /// Schedule supplementary metadata for a single anime after its primary
    /// (AniDB) data has been confirmed or refreshed.
    /// </summary>
    Task ScheduleForAnime(int anidbAnimeID, bool isNew);

    /// <summary>
    /// Schedule supplementary metadata for a batch of anime IDs.
    /// </summary>
    Task ScheduleForAnimes(IEnumerable<int> anidbAnimeIDs, bool isNew);

    /// <summary>
    /// Notify all providers that a series has been permanently removed.
    /// </summary>
    Task OnSeriesRemoved(int anidbAnimeID);
}
