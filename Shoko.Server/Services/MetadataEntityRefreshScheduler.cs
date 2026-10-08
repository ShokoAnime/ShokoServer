using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   Routes the refresh of each stub or stale creator, character, studio and
///   network to the provider taking its source and kind, one job per entry.
/// </summary>
/// <remarks>
///   The stores ask here after every write naming these entries, and the
///   scheduled action for the whole library. An entry is due while it is a
///   stub, or once it is older than the provider's
///   <see cref="IMetadataEntityProvider.EntityStaleAfter"/>. Every refresh is
///   stamped on the entry's row, found or not: a stub it missed waits out
///   <see cref="IMetadataEntityProvider.EntityMissRetryAfter"/>, and any other
///   entry the staleness window.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="scheduler">The queue.</param>
/// <param name="jobFactory">Runs a refresh at once, for a caller that waits for it.</param>
/// <param name="creators">The creators.</param>
/// <param name="characters">The characters.</param>
/// <param name="studios">The studios.</param>
/// <param name="networks">The networks.</param>
/// <param name="logger">The logger.</param>
public class MetadataEntityRefreshScheduler(
    IMetadataProviderManager providerManager,
    IQueueScheduler scheduler,
    IJobFactory jobFactory,
    Metadata_CreatorRepository creators,
    Metadata_CharacterRepository characters,
    Metadata_StudioRepository studios,
    Metadata_NetworkRepository networks,
    ILogger<MetadataEntityRefreshScheduler> logger
)
{
    #region Kinds

    /// <summary>
    ///   The kinds of entries a provider may refresh one at a time.
    /// </summary>
    public static readonly FrozenSet<MetadataEntityType> EntityKinds = FrozenSet.ToFrozenSet([
        MetadataEntityType.Creator,
        MetadataEntityType.Character,
        MetadataEntityType.Studio,
        MetadataEntityType.Network,
    ]);

    /// <summary>
    ///   The kinds a provider refreshes, read from its declared scope: those
    ///   of <see cref="EntityKinds"/> on its own source.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The kinds, and the pairs that were dropped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    public static (IReadOnlySet<MetadataEntityType> Kinds, IReadOnlyList<(MetadataSource Source, MetadataEntityType EntityType)> Dropped) GetKinds(
        IMetadataEntityProvider provider
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        var scope = provider.EntityScope ?? MetadataEntityScope.Empty;
        var kinds = new HashSet<MetadataEntityType>();
        var dropped = new List<(MetadataSource, MetadataEntityType)>();
        foreach (var (source, entityType) in scope)
        {
            if (source == provider.Source && EntityKinds.Contains(entityType))
                kinds.Add(entityType);
            else
                dropped.Add((source, entityType));
        }

        return (kinds, dropped);
    }

    #endregion

    #region Routing

    /// <summary>
    ///   The enabled provider refreshing one source's entries of a kind.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns>The provider, or <c>null</c> when none does.</returns>
    public MetadataProviderInfo? GetProvider(MetadataSource source, MetadataEntityType entityType)
        => EntityKinds.Contains(entityType)
            ? providerManager.MetadataProviders.FirstOrDefault(info =>
                info.Source == source && info.Provider is IMetadataEntityProvider && info.EnabledEntityTypes.Contains(entityType))
            : null;

    /// <summary>
    ///   Whether a stored entry is due a refresh from its provider: a stub
    ///   not asked for yet, a stub the last refresh missed once the miss
    ///   window has passed, or an entry older than the provider's window.
    /// </summary>
    /// <remarks>
    ///   A stub still a stub after a refresh is a miss, as a refresh that
    ///   finds the entry saves it.
    /// </remarks>
    /// <param name="row">The entry's row.</param>
    /// <param name="provider">The provider refreshing it.</param>
    /// <param name="now">The time to measure from.</param>
    /// <returns><c>true</c> when it is due.</returns>
    public bool IsDue(IMetadataStubRow row, IMetadataEntityProvider provider, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(provider);
        var window = provider.EntityStaleAfter;
        var attemptedAt = row.LastRefreshedAt;
        if (row.LastUpdatedAt is not { } updatedAt)
            return attemptedAt is not { } attempted || ((provider.EntityMissRetryAfter ?? window) is { } retry && now - attempted >= retry);

        if (window is not { } staleAfter)
            return false;

        var last = attemptedAt is { } attempt && attempt > updatedAt ? attempt : updatedAt;
        return now - last >= staleAfter;
    }

    /// <summary>
    ///   Queues a refresh of each entry among those given that is due, from
    ///   the provider taking its source and kind. Called by the stores once
    ///   they have written the credits and links naming the entries.
    /// </summary>
    /// <remarks>
    ///   The jobs are queued in the background, and a failure to queue is
    ///   logged: the scheduled action catches up.
    /// </remarks>
    /// <param name="rows">The entries' rows.</param>
    /// <returns>The entries a refresh is queued for.</returns>
    public IReadOnlyList<MetadataGuid> ScheduleDue(IEnumerable<IMetadataStubRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var due = GetDue(rows, DateTime.Now);
        if (due.Count is 0)
            return [];

        _ = QueueInBackground(due);
        return [.. due.Select(pair => pair.ID)];
    }

    /// <summary>
    ///   Queues a refresh of every stub and stale entry something names, of
    ///   every source a provider refreshes these entries for.
    /// </summary>
    /// <param name="progress">Takes the share queued.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many refreshes were queued.</returns>
    public async Task<int> ScheduleAllDue(IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var rows = providerManager.MetadataProviders
            .Where(info => info.Provider is IMetadataEntityProvider)
            .SelectMany(info => info.EnabledEntityTypes.Where(EntityKinds.Contains).Select(kind => (info.Source, Kind: kind)))
            .Distinct()
            .SelectMany(pair => GetRows(pair.Source, pair.Kind))
            .Where(row => row.LastOrphanedAt is null)
            .ToList();
        var due = GetDue(rows, now);
        var items = new ItemProgress(progress, due.Count);
        items.Report(0);
        foreach (var (info, id) in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatch(info, id, force: false, prioritize: false, immediate: false).ConfigureAwait(false);
            items.Increment();
        }

        if (due.Count > 0)
            logger.LogInformation("Queued the refresh of {Count} stub or stale people, studios and networks.", due.Count);

        return due.Count;
    }

    /// <summary>
    ///   Queues a refresh of one entry from the provider taking its source and
    ///   kind, or runs it now and waits for it.
    /// </summary>
    /// <param name="id">The creator, character, studio or network.</param>
    /// <param name="force">Whether to refresh it however fresh it is.</param>
    /// <param name="immediate">Whether to run it now and wait for it.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest even though it is not forced.</param>
    /// <returns>
    ///   <c>true</c> when it was queued or ran, or
    ///   <c>false</c> when no enabled provider refreshes it, or it
    ///   was asked to run at once while it cannot.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    public Task<bool> ScheduleRefresh(MetadataGuid id, bool force = false, bool immediate = false, bool prioritize = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (GetProvider(id.Source, id.EntityType) is not { } info)
        {
            logger.LogDebug("Not refreshing {Entry}: no enabled provider refreshes it.", id);
            return Task.FromResult(false);
        }

        return Dispatch(info, id, force, force || prioritize, immediate);
    }

    /// <summary>
    ///   Looks up the stored row of an entry.
    /// </summary>
    /// <param name="id">The creator, character, studio or network.</param>
    /// <returns>The row, or <c>null</c> when it is not stored.</returns>
    public IMetadataStubRow? GetRow(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Creator ? creators.GetByProviderID(id.Source, id.ID)
            : id.EntityType == MetadataEntityType.Character ? characters.GetByProviderID(id.Source, id.ID)
            : id.EntityType == MetadataEntityType.Studio ? studios.GetByProviderID(id.Source, id.ID)
            : id.EntityType == MetadataEntityType.Network ? networks.GetByProviderID(id.Source, id.ID)
            : null;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   The entries among those given that are due, each once, with the
    ///   provider refreshing it.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="now">The time to measure from.</param>
    /// <returns>The entries and their providers.</returns>
    private List<(MetadataProviderInfo Info, MetadataGuid ID)> GetDue(IEnumerable<IMetadataStubRow> rows, DateTime now)
    {
        var providers = new Dictionary<(MetadataSource, MetadataEntityType), MetadataProviderInfo?>();
        var seen = new HashSet<MetadataGuid>();
        var due = new List<(MetadataProviderInfo, MetadataGuid)>();
        foreach (var row in rows)
        {
            var id = row.ID;
            if (!seen.Add(id))
                continue;

            var key = (id.Source, id.EntityType);
            if (!providers.TryGetValue(key, out var info))
                providers[key] = info = GetProvider(id.Source, id.EntityType);

            if (info?.Provider is IMetadataEntityProvider provider && IsDue(row, provider, now))
                due.Add((info, id));
        }

        return due;
    }

    /// <summary>
    ///   The stored rows of one source's entries of a kind.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind.</param>
    /// <returns>The rows.</returns>
    private IEnumerable<IMetadataStubRow> GetRows(MetadataSource source, MetadataEntityType entityType)
        => entityType == MetadataEntityType.Creator ? creators.GetBySource(source)
            : entityType == MetadataEntityType.Character ? characters.GetBySource(source)
            : entityType == MetadataEntityType.Studio ? studios.GetBySource(source)
            : entityType == MetadataEntityType.Network ? networks.GetBySource(source)
            : [];

    /// <summary>
    ///   Queues the refreshes, logging a failure rather than failing the
    ///   write that asked for them.
    /// </summary>
    /// <param name="due">The entries and their providers.</param>
    /// <returns>A task that completes once they are queued.</returns>
    private async Task QueueInBackground(List<(MetadataProviderInfo Info, MetadataGuid ID)> due)
    {
        foreach (var (info, id) in due)
        {
            try
            {
                await Dispatch(info, id, force: false, prioritize: false, immediate: false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to queue the refresh of {Entry} from {Provider}.", id, info.Name);
            }
        }
    }

    /// <summary>
    ///   Queues a provider's refresh of one entry, or runs it now.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="id">The entry.</param>
    /// <param name="force">Whether to refresh it however fresh it is.</param>
    /// <param name="prioritize">Whether to queue it ahead of the rest.</param>
    /// <param name="immediate">Whether to run it now and wait for it.</param>
    /// <returns>
    ///   <c>true</c> once it is queued or has run, or
    ///   <c>false</c> when the provider has no such job, or it was
    ///   to run now while the queue holds its jobs back.
    /// </returns>
    private async Task<bool> Dispatch(MetadataProviderInfo info, MetadataGuid id, bool force, bool prioritize, bool immediate)
    {
        if (MetadataProviderJobs.GetEntityRefreshJobType(info.Provider.GetType()) is not { } jobType)
            return false;

        return await MetadataProviderScheduler.Dispatch(scheduler, jobFactory, logger, info, jobType, job =>
        {
            var refresh = (IMetadataEntityRefreshJob)job;
            refresh.EntityID = id.ToString();
            refresh.Force = force;
        }, prioritize, immediate).ConfigureAwait(false);
    }

    #endregion
}
