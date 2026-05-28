using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Filters;

public class MetadataFilteringService(
    IFilteringEngine engine,
    AnimeGroupRepository groupRepository,
    AnimeSeriesRepository seriesRepository,
    ILogger<MetadataFilteringService> logger
) : IMetadataFilteringService
{
    public IFilteringEngine Engine => engine;

    public IReadOnlyList<IShokoGroup> FilterGroups(IFilterPreset filter, IUser user, DateTime? time = null, bool skipSorting = false)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(user);

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
        var groupIds = tuples.Select(t => t.GroupID).Distinct().Where(id => id != 0).ToHashSet();

        return groupIds
            .Select(groupRepository.GetByID)
            .Where(g => g is not null)
            .Cast<IShokoGroup>()
            .ToList();
    }

    public IReadOnlyList<IShokoSeries> FilterSeries(IFilterPreset filter, IUser user, DateTime? time = null, bool skipSorting = false)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(user);

        var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);

        return tuples
            .Select(t => seriesRepository.GetByID(t.SeriesID))
            .Where(s => s is not null)
            .Cast<IShokoSeries>()
            .ToList();
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoGroup>> BatchFilterGroups<TFilter>(IReadOnlyList<TFilter> filters, IUser user, DateTime? time = null, bool skipSorting = false)
        where TFilter : IFilterPreset
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(user);

        var results = new Dictionary<TFilter, IReadOnlyList<IShokoGroup>>();

        foreach (var filter in filters.Where(f => !f.IsDirectory))
        {
            try
            {
                var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);
                var groupIds = tuples.Select(t => t.GroupID).Distinct().Where(id => id != 0).ToHashSet();

                var groups = groupIds
                    .Select(groupRepository.GetByID)
                    .Where(g => g is not null)
                    .Cast<IShokoGroup>()
                    .ToList();

                results[filter] = groups;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating batch filter {FilterType}", filter.GetType().Name);
                results[filter] = [];
            }
        }

        foreach (var filter in filters.Where(f => f.IsDirectory).Except(results.Keys))
            results[filter] = [];

        return results;
    }

    public IReadOnlyDictionary<TFilter, IReadOnlyList<IShokoSeries>> BatchFilterSeries<TFilter>(IReadOnlyList<TFilter> filters, IUser user, DateTime? time = null, bool skipSorting = false)
        where TFilter : IFilterPreset
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(user);

        var results = new Dictionary<TFilter, IReadOnlyList<IShokoSeries>>();

        foreach (var filter in filters.Where(f => !f.IsDirectory))
        {
            try
            {
                var tuples = engine.EvaluateFilterWithTuples(filter, user, time, skipSorting);

                var series = tuples
                    .Select(t => seriesRepository.GetByID(t.SeriesID))
                    .Where(s => s is not null)
                    .Cast<IShokoSeries>()
                    .ToList();

                results[filter] = series;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating batch filter {FilterType}", filter.GetType().Name);
                results[filter] = [];
            }
        }

        foreach (var filter in filters.Where(f => f.IsDirectory).Except(results.Keys))
            results[filter] = [];

        return results;
    }
}
