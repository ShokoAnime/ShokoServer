using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
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

    public IReadOnlyList<IShokoGroup> FilterGroups(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));
        if (filter.IsDirectory)
            return [];

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        return tuples
            .DistinctBy(t => t.GroupID)
            .Select(t => groupRepository.GetByID(t.GroupID))
            .Where(g => g is not null)
            .Cast<IShokoGroup>()
            .ToList();
    }

    public IReadOnlyList<IShokoSeries> FilterSeries(IFilterPreset filter, IUser? user = null, DateTime? time = null, bool skipSorting = false)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var needsUser = (filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false);
        if (needsUser)
            ArgumentNullException.ThrowIfNull(user);
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));
        if (filter.IsDirectory)
            return [];

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        return tuples
            .Select(t => seriesRepository.GetByID(t.SeriesID))
            .Where(s => s is not null)
            .Cast<IShokoSeries>()
            .ToList();
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false)
        where TFilter : IFilterPreset
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
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));

        return new LazyDictionary<TFilter, IReadOnlyList<IShokoGroup>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter.IsDirectory
                    ? new Lazy<IReadOnlyList<IShokoGroup>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoGroup>>(() =>
                        engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                            .DistinctBy(t => t.GroupID)
                            .Select(t => groupRepository.GetByID(t.GroupID))
                            .WhereNotNull()
                            .Cast<IShokoGroup>()
                            .ToList()
                    )
            )
        );
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(IReadOnlyList<TFilter> filters, IUser? user = null, DateTime? time = null, bool skipSorting = false)
        where TFilter : IFilterPreset
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
        if (needsUser && user is not JMMUser)
            throw new ArgumentException("Input user must be of type JMMUser.", nameof(user));

        return new LazyDictionary<TFilter, IReadOnlyList<IShokoSeries>>(
            filters.ToDictionary(
                filter => filter,
                filter => filter.IsDirectory
                    ? new Lazy<IReadOnlyList<IShokoSeries>>(() => [])
                    : new Lazy<IReadOnlyList<IShokoSeries>>(() =>
                        engine.EvaluateFilterWithTuples(filter, user, time, skipSorting)
                            .Select(t => t.SeriesID)
                            .Select(seriesRepository.GetByID)
                            .WhereNotNull()
                            .Cast<IShokoSeries>()
                            .ToList()
                    )
            )
        );
    }
}
