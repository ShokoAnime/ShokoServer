using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Filters.SortingSelectors;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Filters;

public class FilterEvaluator
{
    private readonly AnimeGroupRepository _groups = RepoFactory.AnimeGroup;

    private readonly AnimeSeriesRepository _series = RepoFactory.AnimeSeries;

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
        var user = filter.Expression?.UserDependent ?? false;
        if (user && userID == null)
        {
            throw new ArgumentNullException(nameof(userID));
        }

        var filterables = filter.ApplyAtSeriesLevel switch
        {
            true when user => _series.GetAll().Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value))),
            true => _series.GetAll().Select(a => new FilterableWithID(a.AnimeSeriesID, a.AnimeGroupID, a.ToFilterable())),
            false when user => _groups.GetAll().Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToUserDependentFilterable(userID.Value))),
            false => _groups.GetAll().Select(a => new FilterableWithID(0, a.AnimeGroupID, a.ToFilterable()))
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

    private record FilterableWithID(int SeriesID, int GroupID, Filterable Filterable);

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
