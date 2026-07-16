using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Filters;

public class MetadataFilteringService(
    IFilteringEngine engine,
    AnimeGroupRepository groupRepository,
    AnimeSeriesRepository seriesRepository
) : IMetadataFilteringService
{
    public IFilteringEngine Engine => engine;

    public IReadOnlyList<IShokoGroup> GetAllFilteredGroups(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    )
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (tuples.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var groups = tuples
            .DistinctBy(t => t.GroupID)
            .Select(t => groupRepository.GetByID(t.GroupID))
            .WhereNotNull();
        return OrderByGroup(filter, groups, g => g, user, time, skipSorting, cancellationToken)
            .Cast<IShokoGroup>()
            .ToArray();
    }

    public IReadOnlyList<IShokoSeries> GetAllFilteredSeries(
        IFilter filter,
        IUser? user = null,
        DateTime? time = null,
        bool skipSorting = false,
        CancellationToken cancellationToken = default
    )
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (tuples.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        return tuples
            .Select(t => seriesRepository.GetByID(t.SeriesID))
            .WhereNotNull()
            .Cast<IShokoSeries>()
            .ToArray();
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
        where TFilter : IFilter
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return new Dictionary<TFilter, IReadOnlyList<IShokoGroup>>();
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

        var capturedToken = cancellationToken;
        return new LazyDictionary<TFilter, IReadOnlyList<IShokoGroup>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter is IFilterPreset { IsDirectory: true }
                    ? new Lazy<IReadOnlyList<IShokoGroup>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoGroup>>(() =>
                    {
                        capturedToken.ThrowIfCancellationRequested();
                        return OrderByGroup(
                            filter,
                            engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                                .DistinctBy(t => t.GroupID)
                                .Select(t => groupRepository.GetByID(t.GroupID))
                                .WhereNotNull(),
                            g => g,
                            user,
                            time,
                            skipSorting,
                            capturedToken
                        )
                            .Cast<IShokoGroup>()
                            .ToArray();
                    })
            )
        );
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
        where TFilter : IFilter
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return new Dictionary<TFilter, IReadOnlyList<IShokoSeries>>();
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

        var capturedToken = cancellationToken;
        return new LazyDictionary<TFilter, IReadOnlyList<IShokoSeries>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter is IFilterPreset { IsDirectory: true }
                    ? new Lazy<IReadOnlyList<IShokoSeries>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoSeries>>(() =>
                    {
                        capturedToken.ThrowIfCancellationRequested();
                        return engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                            .Select(t => t.SeriesID)
                            .Select(seriesRepository.GetByID)
                            .WhereNotNull()
                            .Cast<IShokoSeries>()
                            .ToArray();
                    })
            )
        );
    }

    public IReadOnlyList<FilteredGroupResult> GetAllFilteredGroupsWithChains(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        // Pre-group to avoid O(N²) per-group scans of chains and results.
        var chainsByBottomGroup = BuildGroupIDChains(results).ToDictionary(g => g[^1], g => g);
        var seriesByGroupID = results.ToLookup(t => t.GroupID, t => t.SeriesID);

        var items = chainsByBottomGroup.Keys
            .Select(groupRepository.GetByID)
            .WhereNotNull()
            .Select(group =>
            {
                var groupIDChain = chainsByBottomGroup[group.AnimeGroupID];
                var groupIds = groupIDChain.ToHashSet();
                var seriesIDs = groupIds.SelectMany(id => seriesByGroupID[id]).ToHashSet();
                return new FilteredGroupResult
                {
                    Group = group,
                    GroupIDChains = [groupIDChain],
                    SeriesIDs = seriesIDs,
                };
            });
        return OrderByGroup(filter, items, r => (AnimeGroup)r.Group, user, time, skipSorting, cancellationToken)
            .ToArray();
    }

    public IReadOnlyList<FilteredGroupResult> GetTopLevelFilteredGroups(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var allGroupIDChains = BuildGroupIDChains(results);

        // Pre-group to avoid O(N²) per-group scans of chains and results.
        var chainsByTopGroup = allGroupIDChains
            .GroupBy(chain => chain[0])
            .ToDictionary(g => g.Key, g => (IReadOnlyList<IReadOnlyList<int>>)g.ToArray());
        var seriesByGroupID = results.ToLookup(t => t.GroupID, t => t.SeriesID);

        var items = chainsByTopGroup.Keys
            .Select(groupRepository.GetByID)
            .WhereNotNull()
            .Select(group =>
            {
                var groupIDChains = chainsByTopGroup[group.AnimeGroupID];
                var groupIds = groupIDChains.SelectMany(chain => chain).ToHashSet();
                var seriesIDs = groupIds.SelectMany(id => seriesByGroupID[id]).ToHashSet();
                return new FilteredGroupResult
                {
                    Group = group,
                    GroupIDChains = groupIDChains,
                    SeriesIDs = seriesIDs,
                };
            });
        return OrderByGroup(filter, items, r => (AnimeGroup)r.Group, user, time, skipSorting, cancellationToken)
            .ToArray();
    }

    public IReadOnlyList<FilteredGroupResult> GetFilteredSubGroups(IFilter filter, IShokoGroup parentGroup, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var validGroupIDs = parentGroup.Groups.Select(a => a.ID).ToHashSet();
        var scopedGroupIDChains = BuildGroupIDChains(results)
            .Where(validGroupIDs.Overlaps)
            .ToArray();
        var orderedGroupIDs = scopedGroupIDChains.SelectMany(a => a).ToArray();
        validGroupIDs.IntersectWith(orderedGroupIDs);
        if (validGroupIDs.Count is 0)
            return [];

        var chainsByTopGroup = scopedGroupIDChains
            .GroupBy(chain => chain[0])
            .ToDictionary(g => g.Key, g => (IReadOnlyList<IReadOnlyList<int>>)g.ToArray());
        var seriesByGroupID = results.ToLookup(t => t.GroupID, t => t.SeriesID);

        var items = validGroupIDs
            .OrderBy(a => Array.IndexOf(orderedGroupIDs, a))
            .Select(groupRepository.GetByID)
            .WhereNotNull()
            .Select(group =>
            {
                var groupIDChains = chainsByTopGroup.TryGetValue(group.AnimeGroupID, out var chains)
                    ? chains
                    : (IReadOnlyList<IReadOnlyList<int>>)[];
                var groupIds = groupIDChains.SelectMany(chain => chain).ToHashSet();
                var seriesIDs = groupIds.SelectMany(id => seriesByGroupID[id]).ToHashSet();
                return new FilteredGroupResult
                {
                    Group = group,
                    GroupIDChains = groupIDChains,
                    SeriesIDs = seriesIDs,
                };
            });
        return OrderByGroup(filter, items, r => (AnimeGroup)r.Group, user, time, skipSorting, cancellationToken)
            .ToArray();
    }

    public IReadOnlyList<IShokoSeries> GetFilteredSeriesInGroup(IFilter filter, IShokoGroup group, bool recursive, IUser? user = null, DateTime? time = null, bool skipSorting = false, CancellationToken cancellationToken = default)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        if (!filter.ApplyAtSeriesLevel)
            return (recursive ? group.AllSeries : group.Series)
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .ToList();

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        cancellationToken.ThrowIfCancellationRequested();

        var validGroupIDs = recursive
            ? group.AllGroups.Prepend(group).Select(a => a.ID).ToHashSet()
            : [group.ID];
        var items = results
            .Where(a => validGroupIDs.Contains(a.GroupID))
            .Select(a => seriesRepository.GetByID(a.SeriesID))
            .WhereNotNull();
        if (filter.SortingExpression is null && !skipSorting)
            items = items
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue);
        return items
            .ToArray();
    }

    // Orders a sequence of group-bearing items at the group level. The filter (and its sorting expression) may have
    // been evaluated at the series level, in which case the result tuples — and therefore the implied group order —
    // follow the series sort, not the group sort. That places a group at the rank of its first matching series rather
    // than its own sort key, so e.g. a name-sorted list ends up out of order at the group level. Re-evaluate the sort
    // against each item's group here so the returned groups are ordered by the group's own sort key.
    private static IEnumerable<T> OrderByGroup<T>(IFilter filter, IEnumerable<T> items, Func<T, AnimeGroup> groupSelector, IUser? user, DateTime? time, bool skipSorting, CancellationToken cancellationToken)
    {
        if (skipSorting)
            return items;

        var now = time?.ToLocalTime() ?? DateTime.Now;
        var sort = filter.SortingExpression;
        if (sort is null)
            return items.OrderBy(item => groupSelector(item).SortName);

        var keyed = items.Select(item =>
        {
            var group = groupSelector(item);
            return (item, filterable: (IFilterableInfo)new FilterableAnimeGroup(group, now), userInfo: user is null ? null : (IFilterableUserInfo)new FilterableGroupUserInfo(group, user.ID, now));
        });
        var ordered = sort.Descending
            ? keyed.OrderByDescending(x =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return sort.Evaluate(x.filterable, x.userInfo, now);
            })
            : keyed.OrderBy(x => sort.Evaluate(x.filterable, x.userInfo, now));
        for (var next = sort.Next; next is not null; next = next.Next)
        {
            var expr = next;
            ordered = expr.Descending
                ? ordered.ThenByDescending(x => expr.Evaluate(x.filterable, x.userInfo, now))
                : ordered.ThenBy(x => expr.Evaluate(x.filterable, x.userInfo, now));
        }
        return ordered.Select(x => x.item);
    }

    private IReadOnlyList<IReadOnlyList<int>> BuildGroupIDChains(IEnumerable<(int GroupID, int SeriesID)> results)
        => results
            .DistinctBy(a => a.GroupID)
            .Select(a =>
                groupRepository.GetByID(a.GroupID)?.AllGroupsAbove
                    .Select(b => b.AnimeGroupID)
                    .Reverse()
                    .Append(a.GroupID)
                    .ToArray()
            )
            .WhereNotNull()
            .ToArray();

    private static void EnsureValidFilter(IFilter filter, IUser? user)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
    }
}
