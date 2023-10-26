using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public FilterEvaluator()
    {
        _series = RepoFactory.AnimeSeries;
        _groups = RepoFactory.AnimeGroup;
    }

    public FilterEvaluator(AnimeGroupRepository groups, AnimeSeriesRepository series)
    {
        _groups = groups;
        _series = series;
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
                                       new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value))) ??
                                   Array.Empty<FilterableWithID>().AsParallel(),
            true => _series?.GetAll().AsParallel().Where(a => user?.AllowedSeries(a) ?? true)
                .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable())) ?? Array.Empty<FilterableWithID>().AsParallel(),
            false when needsUser => _groups?.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true)
                                        .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value))) ??
                                    Array.Empty<FilterableWithID>().AsParallel(),
            false => _groups?.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true)
                .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable())) ?? Array.Empty<FilterableWithID>().AsParallel()
        };

        // Filtering
        var filtered = filterables.Where(a => filter.Expression?.Evaluate(a.Filterable) ?? true);

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
        var needsUser = filters.Any(a => (a?.Expression?.UserDependent ?? false) || skipSorting && (a?.SortingExpression?.UserDependent ?? false));
        if (needsUser && userID == null) throw new ArgumentNullException(nameof(userID));

        var user = userID != null ? RepoFactory.JMMUser.GetByID(userID.Value) : null;
        ILookup<int, CrossRef_AniDB_Other> movieDBMappings;
        using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
        {
            movieDBMappings = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDsAndType(session.Wrap(), null, CrossRefType.MovieDB);
        }

        ParallelQuery<FilterableWithID> series = null;
        ParallelQuery<FilterableWithID> seriesUsers = null;
        ParallelQuery<FilterableWithID> groups = null;
        ParallelQuery<FilterableWithID> groupUsers = null;

        var filterables = filters.ToDictionary(filter => filter, filter =>
        {
            var filterNeedsUser = (filter.Expression?.UserDependent ?? false) || skipSorting && (filter?.SortingExpression?.UserDependent ?? false);
            return filter.ApplyAtSeriesLevel switch
            {
                true when filterNeedsUser => seriesUsers ??= _series.GetAll().AsParallel().Where(a => user?.AllowedSeries(a) ?? true)
                    .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value, movieDBMappings))),
                true => series ??= _series.GetAll().AsParallel().Where(a => user?.AllowedSeries(a) ?? true)
                    .Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable(movieDBMappings))),
                false when filterNeedsUser => groupUsers ??= _groups.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true)
                    .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value))),
                false => groups ??= _groups.GetAll().AsParallel().Where(a => user?.AllowedGroup(a) ?? true)
                    .Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable()))
            };
        });

        // Filtering
        var filtered = filterables.SelectMany(a => a.Value.Select(filterable => (Filter: a.Key, FilterableWithID: filterable))).Where(a =>
            (a.Filter.FilterType & GroupFilterType.Directory) == 0 && (a.Filter.Expression?.Evaluate(a.FilterableWithID.Filterable) ?? true));

        // ordering
        var grouped = filtered.GroupBy(a => a.Filter).ToDictionary(a => a.Key, f =>
        {
            var ordered = skipSorting ? f.Select(a => a.FilterableWithID) : OrderFilterables(f.Key, f.Select(a => a.FilterableWithID));

            var result = ordered.GroupBy(a => a.GroupID, a => a.SeriesID);
            if (!f.Key.ApplyAtSeriesLevel)
                result = result.Select(a => new Grouping(a.Key, _series.GetByGroupID(a.Key).Select(ser => ser.AnimeSeriesID).ToArray()));

            return result;
        });

        foreach (var filter in filters.Where(filter => !grouped.ContainsKey(filter)))
            grouped.Add(filter, Array.Empty<IGrouping<int, int>>());

        return grouped;
    }

    private static IOrderedEnumerable<FilterableWithID> OrderFilterables(FilterPreset filter, IEnumerable<FilterableWithID> filtered)
    {
        var nameSorter = new NameSortingSelector();
        var ordered = filter.SortingExpression == null ? filtered.OrderBy(a => nameSorter.Evaluate(a.Filterable)) :
            !filter.SortingExpression.Descending ? filtered.OrderBy(a => filter.SortingExpression.Evaluate(a.Filterable)) :
            filtered.OrderByDescending(a => filter.SortingExpression.Evaluate(a.Filterable));

        var next = filter.SortingExpression?.Next;
        while (next != null)
        {
            var expr = next;
            ordered = !next.Descending ? ordered.ThenBy(a => expr.Evaluate(a.Filterable)) : ordered.ThenByDescending(a => expr.Evaluate(a.Filterable));
            next = next.Next;
        }

        return ordered;
    }

    private record FilterableWithID(int SeriesID, int GroupID, IFilterable Filterable);
    private record UserFilterableWithID(int UserID, int SeriesID, int GroupID, IFilterable Filterable) : FilterableWithID(SeriesID, GroupID, Filterable);

    private record Grouping(int GroupID, int[] SeriesIDs) : IGrouping<int, int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            return ((IEnumerable<int>)SeriesIDs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Key => GroupID;
    }
}
