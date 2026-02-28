using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.SortingSelectors;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Abstractions.User;

#nullable enable
namespace Shoko.Server.Filters;

public class FilterEvaluator(ILogger<FilterEvaluator> _logger, AnimeGroupRepository _groups, AnimeSeriesRepository _series) : IFilterEvaluator
{
    public IReadOnlyList<IGrouping<int, int>> EvaluateFilter(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));
        var now = time?.ToLocalTime() ?? DateTime.Now;
        var shokoUser = user as JMMUser;
        var filterable = filter.ApplyAtSeriesLevel switch
        {
            true when needsUser => _series.GetAll()
                .AsParallel()
                .Where(a => shokoUser?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now))),
            true => _series.GetAll()
                .AsParallel()
                .Where(a => shokoUser?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now))),
            false when needsUser =>
                _groups.GetAll()
                    .AsParallel()
                    .Where(a => shokoUser?.AllowedGroup(a) ?? true)
                    .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now))),
            false => _groups.GetAll()
                .AsParallel()
                .Where(a => shokoUser?.AllowedGroup(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now))),
        };
        var filtered = filterable.Where(a =>
        {
            try
            {
                return filter.Expression?.Evaluate(a.Filterable, a.UserInfo, now) ?? true;
            }
            catch (Exception e)
            {
                _logger.LogError(
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
            ? sorted.GroupBy(a => a.GroupID, a => a.SeriesID)
            : sorted.Select(a => new Grouping(a.GroupID, _series.GetByGroupID(a.GroupID).Select(ser => ser.AnimeSeriesID)));
        return result.ToArray();
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>> BatchPrepareFilters<TFilter>(IReadOnlyList<TFilter> filters, IUser? user, DateTime? time = null, bool skipSorting = false) where TFilter : IFilterPreset
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return new LazyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>>();
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
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));
        var shokoUser = user as JMMUser;
        var now = time?.ToLocalTime() ?? DateTime.Now;
        var series = !hasSeries ? [] : seriesNeedsUser
            ? _series.GetAll()
                .Where(a => shokoUser?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now)))
                .ToArray()
            : _series.GetAll()
                .Where(a => shokoUser?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(now)))
                .ToArray();
        var groups = !hasGroups ? [] : groupsNeedUser
            ? _groups.GetAll()
                .Where(a => shokoUser?.AllowedGroup(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now), a.ToFilterableUserInfo(user!.ID, now)))
                .ToArray()
            : _groups.GetAll()
                .Where(a => shokoUser?.AllowedGroup(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(now)))
                .ToArray();
        var results = new Dictionary<TFilter, Lazy<IReadOnlyList<IGrouping<int, int>>>>();
        foreach (var filter in filters.Where(a => !a.IsDirectory))
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
                        _logger.LogError(
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
                ? sorted.GroupBy(a => a.GroupID, a => a.SeriesID)
                : sorted.Select(a => new Grouping(a.GroupID, _series.GetByGroupID(a.GroupID).Select(ser => ser.AnimeSeriesID)));
            results[filter] = new(() => result.ToArray());
        }

        // Add Directory Filters
        foreach (var filter in filters.Except(results.Keys))
            results.Add(filter, new(() => []));

        return new LazyDictionary<TFilter, IReadOnlyList<IGrouping<int, int>>>(results);
    }

    private static IOrderedEnumerable<FilterableWithID> OrderFilterable(IFilterPreset filter, IEnumerable<FilterableWithID> filtered, DateTime now)
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

    private sealed class LazyDictionary<TKey, TValue>(Dictionary<TKey, Lazy<TValue>>? dictionary = null) : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, Lazy<TValue>> _dictionary = dictionary ?? [];

        public TValue this[TKey key] => _dictionary[key].Value;

        public IEnumerable<TKey> Keys => _dictionary.Keys;

        public IEnumerable<TValue> Values => _dictionary.Values.Select(a => a.Value);

        public int Count => _dictionary.Count;

        public bool ContainsKey(TKey key)
            => _dictionary.ContainsKey(key);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            => _dictionary.Select(a => new KeyValuePair<TKey, TValue>(a.Key, a.Value.Value)).GetEnumerator();

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (_dictionary.TryGetValue(key, out var lazy))
            {
                value = lazy.Value;
                return true;
            }

            value = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
