using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Services.Airing;

#nullable enable
namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    #region Refreshing | Hints

    /// <inheritdoc/>
    public Task ScheduleRefresh(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var key = GetEntityKey(series);
        return ScheduleRefresh(key.Source, DataEntityType.Series, key.ID);
    }

    /// <inheritdoc/>
    public Task ScheduleRefresh(ISeason season)
    {
        ArgumentNullException.ThrowIfNull(season);

        var key = GetEntityKey(season);
        return ScheduleRefresh(key.Source, DataEntityType.Season, key.ID);
    }

    /// <inheritdoc/>
    public Task ScheduleRefresh(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var key = GetEntityKey(episode);
        return ScheduleRefresh(key.Source, DataEntityType.Episode, key.ID);
    }

    /// <summary>
    /// Enqueue the refresh job for every enabled provider and return. The job
    /// is keyed by provider and entity, so ten refresh clicks collapse into one
    /// per provider.
    /// </summary>
    /// <param name="source">The source of the entity to refresh.</param>
    /// <param name="type">The kind of entity to refresh.</param>
    /// <param name="id">The ID of the entity within its source.</param>
    /// <returns>A task that completes once the jobs are queued.</returns>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    private async Task ScheduleRefresh(DataSource source, DataEntityType type, string id)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        foreach (var info in GetAvailableProviders(onlyEnabled: true).ToList())
            await schedulerFactory.Enqueue<RefreshAiringScheduleJob>(job =>
            {
                job.ProviderID = info.ID;
                job.EntitySource = source;
                job.EntityType = type;
                job.EntityID = id;
            }).ConfigureAwait(false);
    }

    #endregion

    #region Refreshing | Waiting

    /// <inheritdoc/>
    public Task<AiringScheduleRefreshResult> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        var key = GetEntityKey(series);
        return RefreshAsync(key.Source, DataEntityType.Series, key.ID, () => GetSchedulesForSeries(series), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AiringScheduleRefreshResult> RefreshAsync(ISeason season, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(season);

        var key = GetEntityKey(season);
        return RefreshAsync(key.Source, DataEntityType.Season, key.ID, () => GetSchedulesForSeason(season), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AiringScheduleRefreshResult> RefreshAsync(IEpisode episode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var key = GetEntityKey(episode);
        return RefreshAsync(
            key.Source,
            DataEntityType.Episode,
            key.ID,
            () => episode.Series is { } series ? GetSchedulesForSeries(series) : [],
            cancellationToken
        );
    }

    /// <summary>
    /// Wait on the same jobs a hint enqueues, and report what each provider
    /// did, together with the entity's schedules afterwards.
    /// </summary>
    /// <param name="source">The source of the entity to refresh.</param>
    /// <param name="type">The kind of entity to refresh.</param>
    /// <param name="id">The ID of the entity within its source.</param>
    /// <param name="schedules">How to read back the entity's schedules once the work is done.</param>
    /// <param name="cancellationToken">The token cancelling the wait.</param>
    /// <returns>What each provider did, and the entity's schedules.</returns>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    private async Task<AiringScheduleRefreshResult> RefreshAsync(
        DataSource source,
        DataEntityType type,
        string id,
        Func<IReadOnlyList<IAiringSchedule>> schedules,
        CancellationToken cancellationToken
    )
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        var providers = GetAvailableProviders(onlyEnabled: true).ToList();
        var results = await Task.WhenAll(providers.Select(info => RefreshWithProvider(info, source, type, id, cancellationToken))).ConfigureAwait(false);
        return new AiringScheduleRefreshResult(results, schedules());
    }

    /// <summary>
    /// Run one provider's refresh through the queue and wait for what it did.
    /// </summary>
    /// <param name="info">The provider to refresh with.</param>
    /// <param name="source">The source of the entity to refresh.</param>
    /// <param name="type">The kind of entity to refresh.</param>
    /// <param name="id">The ID of the entity within its source.</param>
    /// <param name="cancellationToken">The token cancelling the wait.</param>
    /// <returns>What the provider did.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    private async Task<AiringScheduleProviderRefresh> RefreshWithProvider(
        AiringScheduleProviderInfo info,
        DataSource source,
        DataEntityType type,
        string id,
        CancellationToken cancellationToken
    )
    {
        var key = GetRefreshKey(info.ID, source, type, id);
        var waiter = new TaskCompletionSource<AiringScheduleProviderRefresh>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<TaskCompletionSource<AiringScheduleProviderRefresh>> waiters;
        while (true)
        {
            waiters = _refreshWaiters.GetOrAdd(key, _ => []);
            lock (waiters)
            {
                // The last waiter to leave drops the list from the map, so the
                // list we got hold of is only ours to join while it is still the
                // one everyone else will find under the key.
                if (_refreshWaiters.TryGetValue(key, out var current) && ReferenceEquals(current, waiters))
                {
                    waiters.Add(waiter);
                    break;
                }
            }
        }

        try
        {
            await schedulerFactory.EnqueueImmediate<RefreshAiringScheduleJob>(
                job =>
                {
                    job.ProviderID = info.ID;
                    job.EntitySource = source;
                    job.EntityType = type;
                    job.EntityID = id;
                },
                ct: cancellationToken
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelling the wait leaves the queued work alone, so the caller's
            // token is what is honoured here and nowhere else.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue a refresh with provider {ProviderName}.", info.Name);
            return new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.Failed, ex.Message);
        }
        finally
        {
            lock (waiters)
            {
                waiters.Remove(waiter);
                // An entity is refreshed once and then never again for the life
                // of the process, so an emptied list left behind is a permanent
                // entry per provider and entity.
                if (waiters.Count is 0)
                    _refreshWaiters.TryRemove(key, out _);
            }
        }

        // A job that ran without saying anything, because the provider was gone
        // by the time it started, had nothing to do for the entity.
        return waiter.Task.IsCompletedSuccessfully
            ? await waiter.Task.ConfigureAwait(false)
            : new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.Skipped, null);
    }

    /// <summary>
    /// Run one provider's refresh for one entity. This is what the refresh job
    /// calls; providers implement only the work.
    /// </summary>
    /// <param name="providerID">The provider to refresh with.</param>
    /// <param name="source">The source of the entity to refresh.</param>
    /// <param name="type">The kind of entity to refresh.</param>
    /// <param name="id">The ID of the entity within its source.</param>
    /// <param name="cancellationToken">The token cancelling the work.</param>
    /// <returns>What the provider did, which is also published to anyone waiting on it.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal async Task<AiringScheduleProviderRefresh> ExecuteRefreshAsync(
        Guid providerID,
        DataSource source,
        DataEntityType type,
        string id,
        CancellationToken cancellationToken = default
    )
    {
        var key = GetRefreshKey(providerID, source, type, id);
        if (GetProviderInfo(providerID) is not { } info)
            return Publish(key, new AiringScheduleProviderRefresh(providerID, providerID.ToString(), AiringScheduleRefreshState.Failed, "The provider is not registered."));

        var context = new AiringReadContext(this, includeDisabled: true);
        var entity = type switch
        {
            DataEntityType.Series => context.GetSeries(source, id),
            DataEntityType.Season => (IMetadata?)context.GetSeason(source, id),
            DataEntityType.Episode => context.GetEpisode(source, id),
            _ => null,
        };
        if (entity is null)
            return Publish(key, new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.Skipped, null));

        // One shared job type can't vary its attributes per provider, so the
        // provider's own cap is honoured here instead.
        var limit = _refreshLimits.GetOrAdd(providerID, _ => new SemaphoreSlim(Math.Max(1, info.Provider.MaxConcurrentRefreshes)));
        await limit.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var refreshed = entity switch
            {
                ISeries series => await info.Provider.RefreshAsync(series, cancellationToken).ConfigureAwait(false),
                ISeason season => await info.Provider.RefreshAsync(season, cancellationToken).ConfigureAwait(false),
                IEpisode episode => await info.Provider.RefreshAsync(episode, cancellationToken).ConfigureAwait(false),
                _ => false,
            };
            return Publish(key, new AiringScheduleProviderRefresh(
                info.ID,
                info.Name,
                refreshed ? AiringScheduleRefreshState.Refreshed : AiringScheduleRefreshState.Skipped,
                null
            ));
        }
        catch (OperationCanceledException)
        {
            Publish(key, new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.Cancelled, null));
            throw;
        }
        catch (TimeoutException ex)
        {
            return Publish(key, new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.TimedOut, ex.Message));
        }
        catch (Exception ex)
        {
            // A provider that fails is reported, never thrown, so one source
            // going down doesn't sink the others.
            logger.LogError(ex, "Provider {ProviderName} failed to refresh {EntityType} {EntitySource}:{EntityID}.", info.Name, type, source, id);
            return Publish(key, new AiringScheduleProviderRefresh(info.ID, info.Name, AiringScheduleRefreshState.Failed, ex.Message));
        }
        finally
        {
            limit.Release();
        }
    }

    /// <summary>
    /// Hand a refresh result to whoever is waiting on that provider and entity.
    /// </summary>
    /// <param name="key">The refresh's key.</param>
    /// <param name="result">What the provider did.</param>
    /// <returns>The result, so a caller can return it in one line.</returns>
    private AiringScheduleProviderRefresh Publish(string key, AiringScheduleProviderRefresh result)
    {
        if (!_refreshWaiters.TryGetValue(key, out var waiters))
            return result;

        lock (waiters)
            foreach (var waiter in waiters)
                waiter.TrySetResult(result);

        return result;
    }

    /// <summary>
    /// What a refresh is keyed by, which is the provider and the entity and
    /// nothing else.
    /// </summary>
    /// <param name="providerID">The provider.</param>
    /// <param name="source">The source of the entity.</param>
    /// <param name="type">The kind of entity.</param>
    /// <param name="id">The ID of the entity within its source.</param>
    /// <returns>The key.</returns>
    private static string GetRefreshKey(Guid providerID, DataSource source, DataEntityType type, string id)
        => $"{providerID:D}|{source}|{type}|{id}";

    #endregion
}
