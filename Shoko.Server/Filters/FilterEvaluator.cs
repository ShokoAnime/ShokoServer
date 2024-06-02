using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Filters.SortingSelectors;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Filters;

public class FilterEvaluator
{
    private readonly AnimeGroupRepository _groups;

    private readonly AnimeSeriesRepository _series;
    private readonly ILogger<FilterEvaluator> _logger;

    public FilterEvaluator(AnimeGroupRepository groups, AnimeSeriesRepository series, ILogger<FilterEvaluator> logger)
    {
        _groups = groups;
        _series = series;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate the given filter, applying the necessary logic
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="userID"></param>
    /// <returns>SeriesIDs, grouped by the direct parent GroupID</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<IGrouping<int, int>> EvaluateFilter(FilterPreset filter, int? userID)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser && userID == null) throw new ArgumentNullException(nameof(userID));

        var user = userID != null ? RepoFactory.JMMUser.GetByID(userID.Value) : null;

        var filterables = filter.ApplyAtSeriesLevel switch
        {
            true when needsUser => _series?.GetAll().AsParallel().Where(a => user?.AllowedSeries(a) ?? true).Select(a =>
                                       new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(), a.ToFilterableUserInfo(userID.Value))) ??
                                   Array.Empty<FilterableWithID>().AsParallel(),
            true => _series?.GetAll().AsParallel().Where(a => user?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable())) ?? Array.Empty<FilterableWithID>().AsParallel(),
            false when needsUser => _groups?.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true).Select(a =>
                                        new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(), a.ToFilterableUserInfo(userID.Value))) ??
                                    Array.Empty<FilterableWithID>().AsParallel(),
            false => _groups?.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable())) ?? Array.Empty<FilterableWithID>().AsParallel()
        };

        // Filtering
        var errors = new List<Exception>();
        var filtered = filterables.Where(a =>
        {
            try
            {
                return filter.Expression?.Evaluate(a.Filterable, a.UserInfo) ?? true;
            }
            catch (Exception e)
            {
                errors.Add(e);
                return false;
            }
        });

        if (errors.Any())
            _logger.LogError(new AggregateException(errors.DistinctBy(a => a.StackTrace)), "There were one or more errors while evaluating filter: {Filter}",
                filter);

        // ordering
        var ordered = OrderFilterables(filter, filtered);

        var result = ordered.GroupBy(a => a.GroupID, a => a.SeriesID);
        if (!filter.ApplyAtSeriesLevel)
        {
            result = result.Select(a => new Grouping(a.Key, _series.GetByGroupID(a.Key).Select(ser => ser.AnimeSeriesID).ToArray()));
        }

        return result;
    }

    /// <summary>
    /// Evaluate the given filter, applying the necessary logic
    /// </summary>
    /// <param name="filters"></param>
    /// <param name="userID"></param>
    /// <param name="skipSorting"></param>
    /// <returns>SeriesIDs, grouped by the direct parent GroupID</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public Dictionary<FilterPreset, IEnumerable<IGrouping<int, int>>> BatchEvaluateFilters(List<FilterPreset> filters, int? userID, bool skipSorting=false)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (!filters.Any()) return new Dictionary<FilterPreset, IEnumerable<IGrouping<int, int>>>();
        // count it as a user filter if it needs to sort using a user-dependent expression
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
        if (needsUser && userID == null) throw new ArgumentNullException(nameof(userID));

        var user = userID != null ? RepoFactory.JMMUser.GetByID(userID.Value) : null;
        ILookup<int, CrossRef_AniDB_Other> movieDBMappings;
        using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
            movieDBMappings = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDsAndType(session.Wrap(), null, CrossRefType.MovieDB);

        FilterableWithID[] series = null;
        if (hasSeries)
        {
            var allowedSeries = _series.GetAll().Where(a => user?.AllowedSeries(a) ?? true);
            series = seriesNeedsUser
                ? allowedSeries.Select(a =>
                    new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(movieDBMappings), a.ToFilterableUserInfo(userID.Value))).ToArray()
                : allowedSeries.Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(movieDBMappings))).ToArray();
        }

        FilterableWithID[] groups = null;
        if (hasGroups)
        {
            var allowedGroups = _groups.GetAll().Where(a => user?.AllowedGroup(a) ?? true);
            groups = groupsNeedUser
                ? allowedGroups.Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(movieDBMappings), a.ToFilterableUserInfo(userID.Value)))
                    .ToArray()
                : allowedGroups.Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable(movieDBMappings))).ToArray();
        }

        var filterableMap = filters.Where(a => (a.FilterType & GroupFilterType.Directory) == 0)
            .ToDictionary(filter => filter, filter => filter.ApplyAtSeriesLevel switch { true => series, false => groups });

        var results = new Dictionary<FilterPreset, IEnumerable<IGrouping<int, int>>>();
        
        filterableMap.AsParallel().AsUnordered().ForAll(kv =>
        {
            var (filter, filterables) = kv;
            var expression = filter.Expression;
            // Filtering
            var filtered = filterables.AsParallel().AsUnordered().Where(a => expression?.Evaluate(a.Filterable, a.UserInfo) ?? true).ToArray();
            // Sorting
            var ordered = skipSorting ? (IEnumerable<FilterableWithID>)filtered : OrderFilterables(filter, filtered);
            // Building Group -> Series map
            var result = ordered.GroupBy(a => a.GroupID, a => a.SeriesID);
            // Fill Series IDs for filters calculated at the group level
            if (!filter.ApplyAtSeriesLevel)
                result = result.Select(a => new Grouping(a.Key, _series.GetByGroupID(a.Key).Select(ser => ser.AnimeSeriesID)));
            lock(results) results[filter] = result;
        });

        foreach (var filter in filters.Where(filter => !results.ContainsKey(filter)))
            results.Add(filter, Array.Empty<IGrouping<int, int>>());

        return results;
    }

    private static IOrderedEnumerable<FilterableWithID> OrderFilterables(FilterPreset filter, IEnumerable<FilterableWithID> filtered)
    {
        var nameSorter = new NameSortingSelector();
        var ordered = filter.SortingExpression == null ? filtered.OrderBy(a => nameSorter.Evaluate(a.Filterable, a.UserInfo)) :
            !filter.SortingExpression.Descending ? filtered.OrderBy(a => filter.SortingExpression.Evaluate(a.Filterable, a.UserInfo)) :
            filtered.OrderByDescending(a => filter.SortingExpression.Evaluate(a.Filterable, a.UserInfo));

        var next = filter.SortingExpression?.Next;
        while (next != null)
        {
            var expr = next;
            ordered = !next.Descending ? ordered.ThenBy(a => expr.Evaluate(a.Filterable, a.UserInfo)) : ordered.ThenByDescending(a => expr.Evaluate(a.Filterable, a.UserInfo));
            next = next.Next;
        }

        return ordered;
    }

    private record FilterableWithID(int SeriesID, int GroupID, IFilterable Filterable, IFilterableUserInfo UserInfo=null);

    private record Grouping(int GroupID, IEnumerable<int> SeriesIDs) : IGrouping<int, int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            return SeriesIDs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Key => GroupID;
    }
}
