using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Models;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Filters;

public class FilterPresetManager(FilterPresetRepository filterPresetRepository) : IFilterPresetManager
{
    public IReadOnlyList<IStoredFilterPreset> GetTopLevelPresets()
        => filterPresetRepository.GetTopLevel();

    public IStoredFilterPreset? GetPresetById(int filterID)
        => filterPresetRepository.GetByID(filterID);

    public IReadOnlyList<IStoredFilterPreset> GetPresetsByParentPreset(IStoredFilterPreset filterPreset)
        => filterPreset.ID <= 0 || !filterPreset.IsDirectory ? [] : filterPresetRepository.GetByParentID(filterPreset.ID);

    public IStoredFilterPreset CreatePreset(FilterPresetData input)
    {
        int? parentFilterId = input.ParentFilterID.HasValue && input.ParentFilterID.Value > 0
            ? input.ParentFilterID.Value
            : null;
        if (parentFilterId.HasValue && filterPresetRepository.GetByID(parentFilterId.Value) is null)
            throw new KeyNotFoundException($"Parent filter preset with ID {parentFilterId.Value} not found.");

        var fp = new FilterPreset
        {
            Name = input.Name,
            ParentFilterPresetID = parentFilterId,
            FilterType = input.IsDirectory
                ? FilterPresetType.UserDefined | FilterPresetType.Directory
                : FilterPresetType.UserDefined,
            Hidden = input.IsHidden,
            ApplyAtSeriesLevel = input.ApplyAtSeriesLevel,
        };

        if (!input.IsDirectory)
        {
            if (input.Expression is not null)
            {
                if (!input.Expression.GetType().FullName!.StartsWith("Shoko.Abstractions"))
                    throw new ArgumentException($"Expression not part of the Shoko.Abstractions assembly: {input.Expression.GetType().FullName}", nameof(input.Expression));
                fp.Expression = input.Expression;
            }

            if (input.Sorting is not null)
            {
                if (!input.Sorting.GetType().FullName!.StartsWith("Shoko.Abstractions"))
                    throw new ArgumentException($"Sorting expression not part of the Shoko.Abstractions assembly: {input.Sorting.GetType().FullName}", nameof(input.Sorting));
                fp.SortingExpression = input.Sorting;
            }
        }

        filterPresetRepository.Save(fp);
        return fp;
    }

    public IStoredFilterPreset UpdatePreset(IStoredFilterPreset filter, FilterPresetUpdateData input)
    {
        var fp = filterPresetRepository.GetByID(filter.ID) ??
            throw new KeyNotFoundException($"Filter preset with ID '{filter.ID}' was not stored in the database.");

        var updated = false;
        if (input.Name is not null && !string.Equals(fp.Name, input.Name))
        {
            fp.Name = input.Name;
            updated = true;
        }
        if (input.ParentFilterIDSet && input.ParentFilterID != fp.ParentFilterPresetID)
        {
            fp.ParentFilterPresetID = input.ParentFilterID;
            updated = true;
        }
        if (input.IsHidden.HasValue && input.IsHidden.Value != fp.Hidden)
        {
            fp.Hidden = input.IsHidden.Value;
            updated = true;
        }
        if (input.ApplyAtSeriesLevel.HasValue && input.ApplyAtSeriesLevel.Value != fp.ApplyAtSeriesLevel)
        {
            fp.ApplyAtSeriesLevel = input.ApplyAtSeriesLevel.Value;
            updated = true;
        }

        if (!fp.IsDirectory)
        {
            if (input.ExpressionSet)
            {
                if (fp.Expression is null ? input.Expression is not null : !fp.Expression.Equals(input.Expression))
                {
                    if (input.Expression is not null && !input.Expression.GetType().FullName!.StartsWith("Shoko.Abstractions"))
                        throw new ArgumentException($"Expression not part of the Shoko.Abstractions assembly: {input.Expression.GetType().FullName}", nameof(input.Expression));
                    fp.Expression = input.Expression;
                    updated = true;
                }
            }

            if (input.SortingSet)
            {
                if (fp.SortingExpression is null ? input.Sorting is not null : !fp.SortingExpression.Equals(input.Sorting))
                {
                    if (input.Sorting is not null && !input.Sorting.GetType().FullName!.StartsWith("Shoko.Abstractions"))
                        throw new ArgumentException($"Sorting expression not part of the Shoko.Abstractions assembly: {input.Sorting.GetType().FullName}", nameof(input.Sorting));
                    fp.SortingExpression = input.Sorting;
                    updated = true;
                }
            }
        }

        if (updated)
            filterPresetRepository.Save(fp);

        return fp;
    }

    public void DeletePreset(IStoredFilterPreset filter)
    {
        var fp = filterPresetRepository.GetByID(filter.ID);
        if (fp is not null)
            filterPresetRepository.Delete(fp);
    }

    public IFilterExpressionHelp GetHelpForFilterType<T>() where T : FilterExpression
        => GetHelpForFilterType(typeof(T)) ??
            throw new ArgumentException($"Expression not part of the Shoko.Abstractions assembly: {typeof(T).FullName}", nameof(T));

    public IFilterExpressionHelp? GetHelpForFilterType(Type filterType)
        => ExpressionDiscovery.GetExpressionHelp(filterType);

    public IReadOnlyList<IFilterExpressionHelp> GetAvailableFilterExpressions(FilterExpressionGroup? group = null)
        => ExpressionDiscovery.GetExpressionHelp(group);

    public ISortingCriteriaHelp GetHelpForSortingType<T>() where T : SortingExpression
        => GetHelpForSortingType(typeof(T)) ??
            throw new ArgumentException($"Sorting expression not part of the Shoko.Abstractions assembly: {typeof(T).FullName}", nameof(T));

    public ISortingCriteriaHelp? GetHelpForSortingType(Type sortingType)
        => ExpressionDiscovery.GetSortingCriteriaHelp(sortingType);

    public IReadOnlyList<ISortingCriteriaHelp> GetAvailableSortingCriteria()
        => ExpressionDiscovery.GetSortingCriteriaHelp();
}
