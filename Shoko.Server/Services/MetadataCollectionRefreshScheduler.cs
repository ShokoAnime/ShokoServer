using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   Fetches the collections stored films name through the collection
///   provider of their source, one refresh job per collection.
/// </summary>
/// <remarks>
///   A film names its collection by ID only. The movie store asks here after
///   every save, the refresh job after each film it refreshed, the metadata
///   service when a read finds a collection missing, and the scheduled action
///   catching up on missing people, studios, networks and collections. A collection is
///   fetched while a registered provider of its source has the collection kind
///   enabled, and only while it is stored or a linked film names it. It is due
///   when it is not stored, or was not refreshed within
///   <see cref="MetadataRefreshState.FreshFor"/>.
/// </remarks>
/// <param name="providerManager">The registered providers.</param>
/// <param name="scheduler">The queue.</param>
/// <param name="jobFactory">Handed on to the dispatch, which only queues here.</param>
/// <param name="collections">The stored collections.</param>
/// <param name="movies">The stored films, to find those naming a collection.</param>
/// <param name="crossReferences">The links, to tell a linked film.</param>
/// <param name="logger">The logger.</param>
public class MetadataCollectionRefreshScheduler(
    IMetadataProviderManager providerManager,
    IQueueScheduler scheduler,
    IJobFactory jobFactory,
    Metadata_CollectionRepository collections,
    Metadata_MovieRepository movies,
    Lazy<IMetadataCrossReferenceStore> crossReferences,
    ILogger<MetadataCollectionRefreshScheduler> logger
)
{
    #region Routing

    /// <summary>
    ///   The registered provider of a source whose collection kind is enabled.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The provider, or <c>null</c> when none is.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public MetadataProviderInfo? GetProvider(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return providerManager.MetadataProviders.FirstOrDefault(info =>
            info.Source == source && info.Provider is IMetadataCollectionProvider && info.EnabledEntityTypes.Contains(MetadataEntityType.Collection));
    }

    /// <summary>
    ///   Whether the core keeps a collection: one that is stored, or that a
    ///   stored film something links to names.
    /// </summary>
    /// <param name="collectionID">The collection.</param>
    /// <returns><c>true</c> when it does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="collectionID"/> is <c>null</c>.</exception>
    public bool IsWanted(MetadataGuid collectionID)
    {
        ArgumentNullException.ThrowIfNull(collectionID);
        if (collectionID.EntityType != MetadataEntityType.Collection || collectionID.Source.IsCore)
            return false;

        if (collections.GetByProviderID(collectionID.Source, collectionID.ID) is not null)
            return true;

        return movies.GetBySource(collectionID.Source)
            .Where(movie => string.Equals(movie.ExtraData?.CollectionID, collectionID.ID, StringComparison.Ordinal))
            .Any(movie => crossReferences.Value.IsLinked(new(movie.Source, MetadataEntityType.Movie, movie.ProviderID)));
    }

    /// <summary>
    ///   Whether a collection is due a fetch: not stored, or not refreshed
    ///   within <see cref="MetadataRefreshState.FreshFor"/>.
    /// </summary>
    /// <param name="collectionID">The collection.</param>
    /// <returns><c>true</c> when it is due.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="collectionID"/> is <c>null</c>.</exception>
    public bool IsDue(MetadataGuid collectionID)
    {
        ArgumentNullException.ThrowIfNull(collectionID);
        return collections.GetByProviderID(collectionID.Source, collectionID.ID) is not { } row || !MetadataRefreshState.IsFresh(row.LastRefreshedAt);
    }

    #endregion

    #region Scheduling

    /// <summary>
    ///   Queues the fetch of a collection a film names, or a read found
    ///   missing, when it is due and wanted and a provider of its source has
    ///   the collection kind enabled.
    /// </summary>
    /// <remarks>
    ///   The job is queued in the background, merging with one already
    ///   queued, and a failure to queue is logged: the library refresh
    ///   catches up.
    /// </remarks>
    /// <param name="collectionID">The collection, or <c>null</c> for none.</param>
    /// <returns><c>true</c> when the fetch was queued.</returns>
    public bool ScheduleIfDue(MetadataGuid? collectionID)
    {
        if (collectionID is null || collectionID.EntityType != MetadataEntityType.Collection || collectionID.Source.IsCore)
            return false;

        if (GetProvider(collectionID.Source) is not { } info || MetadataProviderJobs.GetRefreshJobType(info.Provider.GetType()) is not { } jobType)
            return false;

        if (!IsDue(collectionID) || !IsWanted(collectionID))
            return false;

        _ = QueueInBackground(info, jobType, collectionID);
        return true;
    }

    /// <summary>
    ///   Queues the fetch of every collection a linked film names that is not
    ///   stored, on every source whose collection provider has the kind on.
    /// </summary>
    /// <param name="progress">Takes the share queued.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many fetches were queued.</returns>
    /// <exception cref="OperationCanceledException">The work was cancelled.</exception>
    public async Task<int> ScheduleAllMissing(IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
    {
        var missing = providerManager.MetadataProviders
            .Select(info => info.Source)
            .Distinct()
            .Where(source => !source.IsCore)
            .Select(source => (Source: source, Info: GetProvider(source)))
            .Where(pair => pair.Info is not null)
            .SelectMany(pair => movies.GetBySource(pair.Source)
                .Select(movie => movie.ExtraData?.CollectionID)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Select(id => (pair.Info!, ID: new MetadataGuid(pair.Source, MetadataEntityType.Collection, id))))
            .Where(pair => collections.GetByProviderID(pair.ID.Source, pair.ID.ID) is null && IsWanted(pair.ID))
            .ToList();
        var items = new ItemProgress(progress, missing.Count);
        items.Report(0);
        var queued = 0;
        foreach (var (info, collectionID) in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MetadataProviderJobs.GetRefreshJobType(info.Provider.GetType()) is { } jobType && await Queue(info, jobType, collectionID).ConfigureAwait(false))
                queued++;

            items.Increment();
        }

        if (queued > 0)
            logger.LogInformation("Queued the fetch of {Count} collections linked films name.", queued);

        return queued;
    }

    /// <summary>
    ///   Queues a provider's refresh of one collection, logging a failure
    ///   rather than failing the save or read that asked for it.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="jobType">The provider's refresh job type.</param>
    /// <param name="collectionID">The collection.</param>
    /// <returns>A task that completes once it is queued.</returns>
    private async Task QueueInBackground(MetadataProviderInfo info, Type jobType, MetadataGuid collectionID)
    {
        try
        {
            await Queue(info, jobType, collectionID).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to queue the fetch of {Collection} from {Provider}.", collectionID, info.Name);
        }
    }

    /// <summary>
    ///   Queues a provider's refresh of one collection, merging with one
    ///   already queued.
    /// </summary>
    /// <param name="info">The provider.</param>
    /// <param name="jobType">The provider's refresh job type.</param>
    /// <param name="collectionID">The collection.</param>
    /// <returns><c>true</c> once it is queued.</returns>
    private async Task<bool> Queue(MetadataProviderInfo info, Type jobType, MetadataGuid collectionID)
    {
        var queued = await MetadataProviderScheduler.Dispatch(
            scheduler,
            jobFactory,
            logger,
            info,
            jobType,
            job =>
            {
                var refresh = (IMetadataRefreshJob)job;
                refresh.EntryID = collectionID.ToString();
                refresh.Apply(MetadataProviderScheduler.FullRefresh());
            },
            prioritize: false,
            immediate: false
        ).ConfigureAwait(false);
        logger.LogDebug("Queued the fetch of {Collection} from {Provider}.", collectionID, info.Name);
        return queued;
    }

    #endregion
}
