using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;

#nullable enable
namespace Shoko.Server.Filters;

public class MetadataFilteringService(
    IFilteringEngine engine,
    AnimeGroupRepository groupRepository,
    AnimeSeriesRepository seriesRepository
) : IMetadataFilteringService
{
    public IFilteringEngine Engine => engine;

    public IReadOnlyList<IShokoGroup> GetAllFilteredGroups(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        return tuples
            .DistinctBy(t => t.GroupID)
            .Select(t => groupRepository.GetByID(t.GroupID))
            .WhereNotNull()
            .Cast<IShokoGroup>()
            .ToArray();
    }

    public IReadOnlyList<IShokoSeries> GetAllFilteredSeries(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        return tuples
            .Select(t => seriesRepository.GetByID(t.SeriesID))
            .WhereNotNull()
            .Cast<IShokoSeries>()
            .ToArray();
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false)
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

        return new LazyDictionary<TFilter, IReadOnlyList<IShokoGroup>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter is IFilterPreset { IsDirectory: true }
                    ? new Lazy<IReadOnlyList<IShokoGroup>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoGroup>>(() =>
                        engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                            .DistinctBy(t => t.GroupID)
                            .Select(t => groupRepository.GetByID(t.GroupID))
                            .WhereNotNull()
                            .Cast<IShokoGroup>()
                            .ToArray()
                    )
            )
        );
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false)
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

        return new LazyDictionary<TFilter, IReadOnlyList<IShokoSeries>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter is IFilterPreset { IsDirectory: true }
                    ? new Lazy<IReadOnlyList<IShokoSeries>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoSeries>>(() =>
                        engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                            .Select(t => t.SeriesID)
                            .Select(seriesRepository.GetByID)
                            .WhereNotNull()
                            .Cast<IShokoSeries>()
                            .ToArray()
                    )
            )
        );
    }

    public IReadOnlyList<FilteredGroupResult> GetTopLevelFilteredGroups(IFilter filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        var allGroupIDChains = BuildGroupIDChains(results);
        var items = allGroupIDChains
            .DistinctBy(chain => chain[0])
            .Select(group => groupRepository.GetByID(group[0]))
            .WhereNotNull()
            .Select(group =>
            {
                var groupIDChains = allGroupIDChains
                    .Where(chain => chain[0] == group.AnimeGroupID)
                    .ToArray();
                var groupIds = groupIDChains
                    .SelectMany(chain => chain)
                    .ToHashSet();
                var seriesIDs = results
                    .Where(tuple => groupIds.Contains(tuple.GroupID))
                    .Select(tuple => tuple.SeriesID)
                    .ToHashSet();
                return new FilteredGroupResult
                {
                    Group = group,
                    GroupIDChains = groupIDChains,
                    SeriesIDs = seriesIDs,
                };
            });
        if (filter.SortingExpression is null && !skipSorting)
            items = items
                .OrderBy(r => ((AnimeGroup)r.Group).SortName);
        return items
            .ToArray();
    }

    public IReadOnlyList<FilteredGroupResult> GetFilteredSubGroups(IFilter filter, IShokoGroup parentGroup, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

        var validGroupIDs = parentGroup.Groups.Select(a => a.ID).ToHashSet();
        var scopedGroupIDChains = BuildGroupIDChains(results)
            .Where(validGroupIDs.Overlaps)
            .ToArray();
        var orderedGroupIDs = scopedGroupIDChains.SelectMany(a => a).ToArray();
        validGroupIDs.IntersectWith(orderedGroupIDs);
        if (validGroupIDs.Count is 0)
            return [];

        var items = validGroupIDs
            .OrderBy(a => Array.IndexOf(orderedGroupIDs, a))
            .Select(groupRepository.GetByID)
            .WhereNotNull()
            .Select(group =>
            {
                var groupIDChains = scopedGroupIDChains
                    .Where(chain => chain[0] == group.AnimeGroupID)
                    .ToArray();
                var groupIds = groupIDChains
                    .SelectMany(chain => chain)
                    .ToHashSet();
                var seriesIDs = results
                    .Where(tuple => groupIds.Contains(tuple.GroupID))
                    .Select(tuple => tuple.SeriesID)
                    .ToHashSet();
                return new FilteredGroupResult
                {
                    Group = group,
                    GroupIDChains = groupIDChains,
                    SeriesIDs = seriesIDs,
                };
            });
        if (filter.SortingExpression is null && !skipSorting)
            items = items
                .OrderBy(r => ((AnimeGroup)r.Group).SortName);
        return items
            .ToArray();
    }

    public IReadOnlyList<IShokoSeries> GetFilteredSeriesInGroup(IFilter filter, IShokoGroup group, bool recursive, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        EnsureValidFilter(filter, user);
        if (filter is IFilterPreset { IsDirectory: true })
            return [];

        if (!filter.ApplyAtSeriesLevel)
            return (recursive ? group.AllSeries : group.Series)
                .Where(a => user?.IsAllowedToSee(a) ?? true)
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .ToList();

        var results = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        if (results.Count is 0)
            return [];

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
