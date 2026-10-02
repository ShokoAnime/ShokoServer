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
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Filters;

public class FilteringEngine(ILogger<FilteringEngine> logger, AnimeGroupRepository groupRepository, AnimeSeriesRepository seriesRepository) : IFilteringEngine
{
    private readonly SemaphoreSlim _filterSemaphore = new(2, 2);

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
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, new FilterableAnimeSeries(a, now), new FilterableSeriesUserInfo(a, user!.LocalID, now))),
            true => seriesRepository.GetAll()
                .AsParallel()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, new FilterableAnimeSeries(a, now))),
            false => groupRepository.GetAll()
                .AsParallel()
                .Select(ToFilterableGroup(user, needsUser, now))
                .OfType<FilterableWithID>(),
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
        }).AsUnordered().WithCancellation(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var sorted = skipSorting
            ? (IEnumerable<FilterableWithID>)filtered
            : OrderFilterable(filter, filtered, now);
        var result = filter.ApplyAtSeriesLevel
            ? sorted.Select(a => (a.GroupID, a.SeriesID))
            : sorted.SelectMany(a => GetVisibleSeriesIDs(a.GroupID, user));

        cancellationToken.ThrowIfCancellationRequested();

        // Only allow executing up to two filters concurrently.
        _filterSemaphore.Wait(cancellationToken);
        try
        {
            return result.ToArray();
        }
        finally
        {
            _filterSemaphore.Release();
        }
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<(int GroupID, int SeriesID)>> BatchPrepareFiltersWithTuples<TFilter>(IReadOnlyList<TFilter> filters, IUser? user, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default) where TFilter : IFilter
        => InternalBatchPrepareFilters(filters, a => a, user, time, skipSorting, cancellationToken);

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>> BatchPrepareFiltersWithGrouping<TFilter>(IReadOnlyList<TFilter> filters, IUser? user, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default) where TFilter : IFilter
        => InternalBatchPrepareFilters(filters, a => a.GroupBy(a => a.GroupID, a => a.SeriesID), user, time, skipSorting, cancellationToken);

    private IReadOnlyDictionary<TFilter, IReadOnlyList<TValue>> InternalBatchPrepareFilters<TFilter, TValue>(
        IReadOnlyList<TFilter> filters,
        Func<IEnumerable<(int GroupID, int SeriesID)>, IEnumerable<TValue>> convert,
        IUser? user,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
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
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, new FilterableAnimeSeries(a, now), new FilterableSeriesUserInfo(a, user!.LocalID, now)))
                .ToArray()
            : seriesRepository.GetAll()
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, new FilterableAnimeSeries(a, now)))
                .ToArray();
        var groups = !hasGroups ? [] : groupRepository.GetAll()
            .Select(ToFilterableGroup(user, groupsNeedUser, now))
            .OfType<FilterableWithID>()
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
                }).AsUnordered().WithCancellation(cancellationToken);

            var sorted = skipSorting
                ? (IEnumerable<FilterableWithID>)filtered
                : OrderFilterable(filter, filtered, now);
            var result = filter.ApplyAtSeriesLevel
                ? sorted.Select(a => (a.GroupID, a.SeriesID))
                : sorted.SelectMany(a => GetVisibleSeriesIDs(a.GroupID, user));
            var capturedToken = cancellationToken;
            results[filter] = new(() =>
            {
                capturedToken.ThrowIfCancellationRequested();
                _filterSemaphore.Wait(capturedToken);
                try
                {
                    return convert(result).ToArray();
                }
                finally
                {
                    _filterSemaphore.Release();
                }
            });
        }

        // Add Directory Filters
        foreach (var filter in filters.Except(results.Keys))
            results.Add(filter, new(() => []));

        return new LazyDictionary<TFilter, IReadOnlyList<TValue>>(results);
    }

    /// <summary>
    ///   Makes a group filterable for the user, or <c>null</c> when the user
    ///   may not see it. A group the user sees only part of is read from the
    ///   series the user may see.
    /// </summary>
    /// <param name="user">The user, if any.</param>
    /// <param name="withUserInfo">Whether to add the user's data.</param>
    /// <param name="now">The time the filters are evaluated at.</param>
    /// <returns>Makes one group filterable.</returns>
    private static Func<AnimeGroup, FilterableWithID?> ToFilterableGroup(IUser? user, bool withUserInfo, DateTime now)
    {
        if (AnimeGroupView.IsUnrestricted(user))
            return group => new FilterableWithID(
                0,
                group.AnimeGroupID,
                new FilterableAnimeGroup(group, now),
                withUserInfo ? new FilterableGroupUserInfo(group, user!.LocalID, now) : null
            );

        return group =>
        {
            var view = AnimeGroupView.For(group, user);
            if (!view.IsVisible)
                return null;

            return new FilterableWithID(
                0,
                group.AnimeGroupID,
                new FilterableAnimeGroup(group, now, view),
                withUserInfo ? new FilterableGroupUserInfo(group, user!.LocalID, now, view) : null
            );
        };
    }

    /// <summary>
    ///   The series directly in a group the user may see, for a group-level
    ///   filter's results.
    /// </summary>
    /// <param name="groupID">The group's ID.</param>
    /// <param name="user">The user, if any.</param>
    /// <returns>The group and series ID pairs.</returns>
    private IEnumerable<(int GroupID, int SeriesID)> GetVisibleSeriesIDs(int groupID, IUser? user)
    {
        var series = seriesRepository.GetByGroupID(groupID);
        return AnimeGroupView.IsUnrestricted(user)
            ? series.Select(ser => (groupID, ser.AnimeSeriesID))
            : series.Where(ser => user!.IsAllowedToSee(ser)).Select(ser => (groupID, ser.AnimeSeriesID));
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
