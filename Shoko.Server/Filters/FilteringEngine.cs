using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting.Selectors;
using Shoko.Abstractions.User;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Filters;

public class FilteringEngine(ILogger<FilteringEngine> logger, AnimeGroupRepository groupRepository, AnimeSeriesRepository seriesRepository) : IFilteringEngine
{
    private readonly Lock _filterLock = new();

    public IReadOnlyList<IGrouping<int, int>> EvaluateFilterWithGrouping(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
        => EvaluateFilterWithTuples(filter, user, time, skipSorting, cancellationToken).GroupBy(a => a.GroupID, a => a.SeriesID).ToArray();

    public IReadOnlyList<(int GroupID, int SeriesID)> EvaluateFilterWithTuples(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var now = time?.ToLocalTime() ?? DateTime.Now;
        var filterable = filter.ApplyAtSeriesLevel switch
        {
            true when needsUser => seriesRepository.GetAll()
                .AsParallel()
                .Where(a => user!.IsAllowedToSee(a))
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now))),
            true => seriesRepository.GetAll()
                .AsParallel()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now))),
            false when needsUser =>
                groupRepository.GetAll()
                    .AsParallel()
                    .Where(a => user!.IsAllowedToSee(a))
                    .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now))),
            false => groupRepository.GetAll()
                .AsParallel()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now))),
        };
        var filtered = filterable.Where(a =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return filter.Expression?.Evaluate(a.Filterable, a.UserInfo, now) ?? true;
            }
            // Don't log and rethrow OperationCanceledExceptions for the caller to handle.
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "There was an error while evaluating filter expression: {Expression} (GroupID={GroupID}, SeriesID={SeriesID})",
                    filter.Expression,
                    a.GroupID,
                    a.SeriesID is 0 ? null : a.SeriesID
                );
                return false;
            }
        });
        var sorted = skipSorting
            ? (IEnumerable<FilterableWithID>)filtered
            : OrderFilterable(filter, filtered, now);
        var result = filter.ApplyAtSeriesLevel
            ? sorted.Select(a => (a.GroupID, a.SeriesID))
            : sorted.SelectMany(a => seriesRepository.GetByGroupID(a.GroupID).Select(ser => (a.GroupID, ser.AnimeSeriesID)));

        cancellationToken.ThrowIfCancellationRequested();

        // Only allow executing one filter at a time.
        lock (_filterLock)
        {
            return result.ToArray();
        }
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<(int GroupID, int SeriesID)>> BatchPrepareFiltersWithTuples<TFilter>(IReadOnlyList<TFilter> filters, IUser? user, DateTime? time = null, bool skipSorting = false) where TFilter : IFilter
        => InternalBatchPrepareFilters(filters, a => a, user, time, skipSorting);

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>> BatchPrepareFiltersWithGrouping<TFilter>(IReadOnlyList<TFilter> filters, IUser? user, DateTime? time = null, bool skipSorting = false) where TFilter : IFilter
        => InternalBatchPrepareFilters(filters, a => a.GroupBy(a => a.GroupID, a => a.SeriesID), user, time, skipSorting);

    private IReadOnlyDictionary<TFilter, IReadOnlyList<TValue>> InternalBatchPrepareFilters<TFilter, TValue>(
        IReadOnlyList<TFilter> filters,
        Func<IEnumerable<(int GroupID, int SeriesID)>, IEnumerable<TValue>> convert,
        IUser? user,
        DateTime? time = null,
        bool skipSorting = false
    ) where TFilter : IFilter
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return new LazyDictionary<TFilter, IReadOnlyList<TValue>>();
        var hasSeries = filters.Any(a => a.ApplyAtSeriesLevel);
        var seriesNeedsUser = hasSeries && filters.Any(a =>
        {
            if (!a.ApplyAtSeriesLevel) return false;
            if (a.Expression?.UserDependent ?? false) return true;
            if (skipSorting) return false;
            return a.SortingExpression?.UserDependent ?? false;
        });
        var hasGroups = filters.Any(a => !a.ApplyAtSeriesLevel);
        var groupsNeedUser = hasGroups && filters.Any(a =>
        {
            if (a.ApplyAtSeriesLevel) return false;
            if (a.Expression?.UserDependent ?? false) return true;
            if (skipSorting) return false;
            return a.SortingExpression?.UserDependent ?? false;
        });
        var needsUser = seriesNeedsUser || groupsNeedUser;
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
        var now = time?.ToLocalTime() ?? DateTime.Now;
        var series = !hasSeries ? [] : seriesNeedsUser
            ? seriesRepository.GetAll()
                .Where(a => user!.IsAllowedToSee(a))
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now)))
                .ToArray()
            : seriesRepository.GetAll()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now)))
                .ToArray();
        var groups = !hasGroups ? [] : groupsNeedUser
            ? groupRepository.GetAll()
                .Where(a => user!.IsAllowedToSee(a))
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now)))
                .ToArray()
            : groupRepository.GetAll()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now)))
                .ToArray();
        var results = new Dictionary<TFilter, Lazy<IReadOnlyList<TValue>>>();
        foreach (var filter in filters.Where(a => a is not IFilterPreset { IsDirectory: true }))
        {
            var filterable = filter.ApplyAtSeriesLevel ? series : groups;
            var expression = filter.Expression;
            var filtered = filterable
                .AsParallel()
                .AsUnordered()
                .Where(a =>
                {
                    try
                    {
                        return expression?.Evaluate(a.Filterable, a.UserInfo, now) ?? true;
                    }
                    catch (Exception e)
                    {
                        logger.LogError(
                            e,
                            "There was an error while evaluating filter expression: {Expression} (GroupID={GroupID}, SeriesID={SeriesID})",
                            expression,
                            a.GroupID,
                            a.SeriesID is 0 ? null : a.SeriesID
                        );
                        return false;
                    }
                });
            var sorted = skipSorting
                ? (IEnumerable<FilterableWithID>)filtered
                : OrderFilterable(filter, filtered, now);
            var result = filter.ApplyAtSeriesLevel
                ? sorted.Select(a => (a.GroupID, a.SeriesID))
                : sorted.SelectMany(a => seriesRepository.GetByGroupID(a.GroupID).Select(ser => (a.GroupID, ser.AnimeSeriesID)));
            results[filter] = new(() =>
            {
                // Only allow executing one filter at a time.
                lock (_filterLock)
                {
                    return convert(result).ToArray();
                }
            });
        }

        // Add Directory Filters
        foreach (var filter in filters.Except(results.Keys))
            results.Add(filter, new(() => []));

        return new LazyDictionary<TFilter, IReadOnlyList<TValue>>(results);
    }

    private static IOrderedEnumerable<FilterableWithID> OrderFilterable(IFilter filter, IEnumerable<FilterableWithID> filtered, DateTime now)
    {
        if (filter.SortingExpression is null)
        {
            var nameSorter = new NameSortingSelector();
            return filtered.OrderBy(a => nameSorter.Evaluate(a.Filterable, a.UserInfo, now));
        }
        var ordered = !filter.SortingExpression.Descending
            ? filtered.OrderBy(a => filter.SortingExpression.Evaluate(a.Filterable, a.UserInfo, now))
            : filtered.OrderByDescending(a => filter.SortingExpression.Evaluate(a.Filterable, a.UserInfo, now));
        var next = filter.SortingExpression?.Next;
        while (next is not null)
        {
            var expr = next;
            ordered = !next.Descending
                ? ordered.ThenBy(a => expr.Evaluate(a.Filterable, a.UserInfo, now))
                : ordered.ThenByDescending(a => expr.Evaluate(a.Filterable, a.UserInfo, now));
            next = next.Next;
        }
        return ordered;
    }

    private record FilterableWithID(int SeriesID, int GroupID, IFilterableInfo Filterable, IFilterableUserInfo? UserInfo = null);

    private record Grouping(int GroupID, IEnumerable<int> SeriesIDs) : IGrouping<int, int>
    {
        [MustDisposeResource]
        public IEnumerator<int> GetEnumerator()
        {
            return SeriesIDs.GetEnumerator();
        }

        [MustDisposeResource]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Key => GroupID;
    }
}
