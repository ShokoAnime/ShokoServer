using System;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Airing;

/// <summary>
/// Cached repository for <see cref="AiringScheduleSweepState"/>, the sweep
/// driver's own bookkeeping.
/// </summary>
/// <remarks>
/// One row per sweeping provider, so the table is as small as the plugin list
/// and is read on every tick of the sweep dispatcher. Cached rather than direct
/// for that reason.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class AiringScheduleSweepStateRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AiringScheduleSweepState, int>(databaseFactory)
{
    private PocoIndex<int, AiringScheduleSweepState, Guid>? _providerIDs;

    /// <inheritdoc/>
    protected override int SelectKey(AiringScheduleSweepState entity)
        => entity.AiringScheduleSweepStateID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _providerIDs = Cache.CreateIndex(state => state.ProviderID);
    }

    /// <summary>
    /// Gets the sweep state for the given provider.
    /// </summary>
    /// <param name="providerID">The ID of the provider.</param>
    /// <returns>The state, or <c>null</c> when the provider has never swept.</returns>
    public AiringScheduleSweepState? GetByProviderID(Guid providerID)
        => _providerIDs!.GetOne(providerID);
}
