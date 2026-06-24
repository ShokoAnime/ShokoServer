#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services;

/// <summary>
/// Dispatches supplementary metadata work to all registered
/// <see cref="ISupplementaryMetadataProvider"/> implementations.
/// </summary>
public class SupplementaryMetadataService : ISupplementaryMetadataService
{
    private IReadOnlyList<ISupplementaryMetadataProvider> _providers = [];

    public void AddParts(IEnumerable<ISupplementaryMetadataProvider> providers)
        => _providers = providers.ToList();

    /// <inheritdoc />
    public async Task ScheduleForAnime(int anidbAnimeID, bool isNew)
    {
        foreach (var provider in _providers)
            await provider.ScheduleForAnime(anidbAnimeID, isNew);
    }

    /// <inheritdoc />
    public async Task ScheduleForAnimes(IEnumerable<int> anidbAnimeIDs, bool isNew)
    {
        foreach (var animeID in anidbAnimeIDs)
            await ScheduleForAnime(animeID, isNew);
    }

    /// <inheritdoc />
    public async Task OnSeriesRemoved(int anidbAnimeID)
    {
        foreach (var provider in _providers)
            await provider.OnSeriesRemoved(anidbAnimeID);
    }
}
