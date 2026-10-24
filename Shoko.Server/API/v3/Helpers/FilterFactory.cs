using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Models;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Helpers;

public class FilterFactory
{
    private static readonly Dictionary<string, Type> s_expressionTypes;
    private static readonly Dictionary<string, Type> s_sortingTypes;
    private readonly HttpContext _context;
    private readonly IFilteringEngine _evaluator;

    static FilterFactory()
    {
        var allTypes = ReflectionUtils.ScannableAssemblies().SelectMany(a => a.GetTypes()).ToList();
        s_expressionTypes = allTypes
            .Where(a => a != typeof(FilterExpression) && !a.IsGenericType && typeof(FilterExpression).IsAssignableFrom(a) &&
                        !typeof(SortingExpression).IsAssignableFrom(a))
            .ToDictionary(a => a.Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector"));
        s_sortingTypes = allTypes
            .Where(a => a != typeof(FilterExpression) && !a.IsAbstract && !a.IsGenericType && typeof(SortingExpression).IsAssignableFrom(a))
            .ToDictionary(a => a.Name.TrimEnd("SortingSelector"));
    }

    public FilterFactory(IHttpContextAccessor context, IFilteringEngine evaluator)
    {
        _context = context.HttpContext!;
        _evaluator = evaluator;
    }

    public Filter GetFilter(FilterPreset groupFilter, bool fullModel = false, bool includeEmpty = false)
    {
        var user = _context.GetUser();
        var filter = new Filter
        {
            IDs = new Filter.FilterIDs
            {
                ID = groupFilter.FilterPresetID,
                ParentFilter = groupFilter.ParentFilterPresetID
            },
            Name = groupFilter.Name,
            IsLocked = groupFilter.Locked,
            IsDirectory = groupFilter.IsDirectory,
            IsHidden = groupFilter.Hidden,
            ApplyAtSeriesLevel = groupFilter.ApplyAtSeriesLevel,
        };

        if (fullModel)
        {
            filter.Expression = GetExpressionTree(groupFilter.Expression);
            filter.Sorting = GetSortingCriteria(groupFilter.SortingExpression);
        }

        filter.Size = filter.IsDirectory
            ? RepoFactory.FilterPreset.GetByParentID(groupFilter.FilterPresetID).Count
            // Only count top level groups
            : _evaluator.EvaluateFilterWithGrouping(groupFilter, user).Count(a =>
            {
                var group = RepoFactory.AnimeGroup.GetByID(a.Key);
                return group is { AnimeGroupParentID: null } && (includeEmpty || AnimeGroupView.For(group, user).AllSeries
                    .Any(s => s.AnimeEpisodes.Any(e => e.VideoLocals.Count > 0)));
            });
        return filter;
    }

    public IEnumerable<Filter> GetFilters(List<FilterPreset> groupFilters, bool fullModel = false)
    {
        var user = _context.GetUser();
        var evaluate = groupFilters.Any(a => !a.IsDirectory);
        var results = evaluate ? _evaluator.BatchPrepareFiltersWithGrouping(groupFilters, user, skipSorting: true, cancellationToken: _context.RequestAborted) : null;
        var filters = groupFilters.Select(groupFilter =>
        {
            var filter = new Filter
            {
                IDs = new Filter.FilterIDs
                {
                    ID = groupFilter.FilterPresetID,
                    ParentFilter = groupFilter.ParentFilterPresetID
                },
                Name = groupFilter.Name,
                IsLocked = groupFilter.Locked,
                IsDirectory = groupFilter.IsDirectory,
                IsHidden = groupFilter.Hidden,
                ApplyAtSeriesLevel = groupFilter.ApplyAtSeriesLevel,
            };

            if (fullModel)
            {
                filter.Expression = GetExpressionTree(groupFilter.Expression);
                filter.Sorting = GetSortingCriteria(groupFilter.SortingExpression);
            }

            filter.Size = filter.IsDirectory ? RepoFactory.FilterPreset.GetByParentID(groupFilter.FilterPresetID).Count : results?[groupFilter].Count ?? 0;
            return filter;
        });

        return filters;
    }

    public Filter.FilterCondition? GetExpressionTree(FilterExpression? expression)
    {
        if (expression is null) return null;
        var result = new Filter.FilterCondition
        {
            Type = expression.GetType().Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").Trim()
        };

        // Left/First
        switch (expression)
        {
            case IWithExpressionParameter left:
                result.Left = GetExpressionTree(left.Left);
                break;
            case IWithDateSelectorParameter left:
                result.Left = GetExpressionTree(left.Left);
                break;
            case IWithNumberSelectorParameter left:
                result.Left = GetExpressionTree(left.Left);
                break;
            case IWithStringSelectorParameter left:
                result.Left = GetExpressionTree(left.Left);
                break;
            case IWithStringSetSelectorParameter left:
                result.Left = GetExpressionTree(left.Left);
                break;
        }

        // Parameters
        switch (expression)
        {
            case IWithBoolParameter parameter:
                result.Parameter = parameter.Parameter?.ToString().ToLower();
                break;
            case IWithStringParameter parameter:
                result.Parameter = parameter.Parameter;
                break;
            case IWithNumberParameter parameter:
                result.Parameter = parameter.Parameter?.ToString(CultureInfo.CurrentCulture);
                break;
            case IWithDateParameter parameter:
                result.Parameter = parameter.Parameter?.ToUniversalTime().ToString("u");
                break;
            case IWithTimeSpanParameter parameter:
                result.Parameter = parameter.Parameter?.ToString("G");
                break;
            case IWithStringSetParameter parameter:
                result.Parameter = parameter.Parameter?.Join("|||");
                break;
        }

        // Right/Second
        switch (expression)
        {
            case IWithSecondExpressionParameter right:
                result.Right = GetExpressionTree(right.Right);
                break;
            case IWithSecondDateSelectorParameter right:
                result.Right = GetExpressionTree(right.Right);
                break;
            case IWithSecondStringSelectorParameter right:
                result.Right = GetExpressionTree(right.Right);
                break;
            case IWithSecondNumberSelectorParameter right:
                result.Right = GetExpressionTree(right.Right);
                break;
        }

        // Second Parameter
        switch (expression)
        {
            case IWithSecondStringParameter right:
                result.SecondParameter = right.SecondParameter;
                break;
        }

        return result;
    }

    public FilterExpression<T>? GetExpressionTree<T>(Filter.FilterCondition? condition)
    {
        if (condition is null) return null;
        if (!s_expressionTypes.TryGetValue(condition.Type.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").Trim(), out var type))
            throw new ArgumentException($"FilterCondition type {condition.Type} was not found");
        var result = (FilterExpression<T>)Activator.CreateInstance(type)!;

        // Left/First
        switch (result)
        {
            case IWithExpressionParameter left:
                left.Left = GetExpressionTree<bool>(condition.Left);
                break;
            case IWithDateSelectorParameter left:
                left.Left = GetExpressionTree<DateTime?>(condition.Left);
                break;
            case IWithNumberSelectorParameter left:
                left.Left = GetExpressionTree<double>(condition.Left);
                break;
            case IWithStringSelectorParameter left:
                left.Left = GetExpressionTree<string>(condition.Left);
                break;
            case IWithStringSetSelectorParameter left:
                left.Left = GetExpressionTree<IReadOnlySet<string>>(condition.Left);
                break;
        }

        // Parameters
        switch (result)
        {
            case IWithBoolParameter parameter:
                parameter.Parameter = bool.TryParse(condition.Parameter, out var flag) ? flag : null;
                break;
            case IWithStringParameter parameter:
                parameter.Parameter = condition.Parameter;
                break;
            case IWithNumberParameter parameter:
                parameter.Parameter = double.TryParse(condition.Parameter, out var number) ? number : null;
                break;
            case IWithDateParameter parameter:
                parameter.Parameter = DateTime.TryParse(condition.Parameter, out var date) ? date : null;
                break;
            case IWithTimeSpanParameter parameter:
                parameter.Parameter = TimeSpan.TryParse(condition.Parameter, out var timeSpan) ? timeSpan : null;
                break;
            case IWithStringSetParameter parameter:
                parameter.Parameter = condition.Parameter switch
                {
                    null => null,
                    "" => [],
                    var text => text.Split("|||").ToHashSet(),
                };
                break;
        }

        // Right/Second
        switch (result)
        {
            case IWithSecondExpressionParameter right:
                right.Right = GetExpressionTree<bool>(condition.Right);
                break;
            case IWithSecondDateSelectorParameter right:
                right.Right = GetExpressionTree<DateTime?>(condition.Right);
                break;
            case IWithSecondStringSelectorParameter right:
                right.Right = GetExpressionTree<string>(condition.Right);
                break;
            case IWithSecondNumberSelectorParameter right:
                right.Right = GetExpressionTree<double>(condition.Right);
                break;
        }

        // Second Parameter
        switch (result)
        {
            case IWithSecondStringParameter right:
                right.SecondParameter = condition.SecondParameter;
                break;
        }

        return result;
    }

    public Filter.SortingCriteria GetSortingCriteria(SortingExpression? expression)
    {
        if (expression == null) return new Filter.SortingCriteria { Type = "Name", IsInverted = false };

        var result = new Filter.SortingCriteria
        {
            Type = expression.GetType().Name.TrimEnd("SortingSelector"),
            IsInverted = expression.Descending,
            Parameter = (expression as IWithStringParameter)?.Parameter
        };

        var currentCriteria = result;
        var currentExpression = expression;
        while (currentExpression.Next != null)
        {
            currentExpression = currentExpression.Next;
            currentCriteria.Next = new Filter.SortingCriteria
            {
                Type = currentExpression.GetType().Name.TrimEnd("SortingSelector"),
                IsInverted = currentExpression.Descending,
                Parameter = (currentExpression as IWithStringParameter)?.Parameter
            };
            currentCriteria = currentCriteria.Next;
        }

        return result;
    }

    public SortingExpression GetSortingCriteria(Filter.SortingCriteria criteria)
    {
        if (!s_sortingTypes.TryGetValue(criteria.Type, out var type))
            throw new ArgumentException($"SortingExpression type {criteria.Type}Selector was not found");
        var result = (SortingExpression)Activator.CreateInstance(type)!;
        result.Descending = criteria.IsInverted;
        if (result is IWithStringParameter parameterized && criteria.Parameter is not null)
            parameterized.Parameter = criteria.Parameter;

        if (criteria.Next != null) result.Next = GetSortingCriteria(criteria.Next);

        return result;
    }

    public Filter.Input.CreateOrUpdateFilterBody GetPostModel(FilterPreset groupFilter)
    {
        var result = new Filter.Input.CreateOrUpdateFilterBody
        {
            Name = groupFilter.Name,
            ParentID = groupFilter.ParentFilterPresetID,
            IsDirectory = groupFilter.IsDirectory,
            IsHidden = groupFilter.Hidden,
            ApplyAtSeriesLevel = groupFilter.ApplyAtSeriesLevel
        };

        if (!result.IsDirectory)
        {
            result.Expression = GetExpressionTree(groupFilter.Expression);
            result.Sorting = GetSortingCriteria(groupFilter.SortingExpression);
        }

        return result;
    }

    public FilterPreset? GetFilterPreset(Filter.Input.CreateOrUpdateFilterBody filter, ModelStateDictionary? modelState = null, FilterPreset? existing = null)
    {
        existing ??= new FilterPreset();
        if (!ValidateFilterBody(filter, modelState, existing))
            return null;

        existing.ParentFilterPresetID = filter.ParentID;
        existing.FilterType = filter.IsDirectory ? FilterPresetType.UserDefined | FilterPresetType.Directory : FilterPresetType.UserDefined;
        existing.Name = filter.Name ?? string.Empty;
        existing.Hidden = filter.IsHidden;
        existing.ApplyAtSeriesLevel = filter.ApplyAtSeriesLevel;
        if (!filter.IsDirectory)
        {
            if (filter.Expression != null) existing.Expression = GetExpressionTree<bool>(filter.Expression);
            if (filter.Sorting != null) existing.SortingExpression = GetSortingCriteria(filter.Sorting);
        }

        return existing;
    }

    /// <summary>
    /// Validates a body for a new filter preset and turns it into the data to
    /// create it with.
    /// </summary>
    /// <param name="filter">The body.</param>
    /// <param name="modelState">Collects the validation errors.</param>
    /// <returns>The data, or <c>null</c> when the body is invalid.</returns>
    public FilterPresetData? GetFilterPresetData(Filter.Input.CreateOrUpdateFilterBody filter, ModelStateDictionary modelState)
    {
        if (!ValidateFilterBody(filter, modelState, null))
            return null;

        return new()
        {
            Name = filter.Name ?? string.Empty,
            ParentFilterID = filter.ParentID,
            IsDirectory = filter.IsDirectory,
            IsHidden = filter.IsHidden,
            ApplyAtSeriesLevel = filter.ApplyAtSeriesLevel,
            Expression = !filter.IsDirectory && filter.Expression != null ? GetExpressionTree<bool>(filter.Expression) : null,
            Sorting = !filter.IsDirectory && filter.Sorting != null ? GetSortingCriteria(filter.Sorting) : null,
        };
    }

    /// <summary>
    /// Validates a body for a stored filter preset and turns it into the
    /// update to apply to it.
    /// </summary>
    /// <param name="filter">The body.</param>
    /// <param name="modelState">Collects the validation errors.</param>
    /// <param name="existing">The stored preset.</param>
    /// <returns>The update, or <c>null</c> when the body is invalid.</returns>
    public FilterPresetUpdateData? GetFilterPresetUpdateData(Filter.Input.CreateOrUpdateFilterBody filter, ModelStateDictionary modelState, FilterPreset existing)
    {
        if (!ValidateFilterBody(filter, modelState, existing))
            return null;

        var update = new FilterPresetUpdateData
        {
            Name = filter.Name ?? string.Empty,
            ParentFilterID = filter.ParentID,
            IsHidden = filter.IsHidden,
            ApplyAtSeriesLevel = filter.ApplyAtSeriesLevel,
        };
        if (!existing.IsDirectory)
        {
            if (filter.Expression != null) update.Expression = GetExpressionTree<bool>(filter.Expression);
            if (filter.Sorting != null) update.Sorting = GetSortingCriteria(filter.Sorting);
        }

        return update;
    }

    /// <summary>
    /// Applies the API's rules for a filter preset body: no locked preset or
    /// parent, a parent that is a directory, no conditions or sorting on a
    /// directory, and no stored preset becoming a directory or stopping being
    /// one.
    /// </summary>
    /// <param name="filter">The body.</param>
    /// <param name="modelState">Collects the validation errors.</param>
    /// <param name="existing">The preset the body is for, if any.</param>
    /// <returns><c>true</c> when the body is valid.</returns>
    private static bool ValidateFilterBody(Filter.Input.CreateOrUpdateFilterBody filter, ModelStateDictionary? modelState, FilterPreset? existing)
    {
        var isValid = true;
        void AddError(string key, string message)
        {
            isValid = false;
            modelState?.AddModelError(key, message);
        }

        if (existing is { Locked: true })
            AddError("IsLocked", "Filter is locked.");

        // Defer to `null` if the id is `0`.
        if (filter.ParentID is 0)
            filter.ParentID = null;

        if (filter.ParentID.HasValue)
        {
            var parentFilter = RepoFactory.FilterPreset.GetByID(filter.ParentID.Value);
            if (parentFilter == null)
            {
                AddError(nameof(filter.ParentID), $"Unable to find parent filter with id {filter.ParentID.Value}");
            }
            else
            {
                if (parentFilter.Locked)
                    AddError(nameof(filter.ParentID), "Unable to add a sub-filter to a filter that is locked.");

                if (!parentFilter.IsDirectory)
                    AddError(nameof(filter.ParentID), "Unable to add a sub-filter to a filter that is not a directorty filter.");
            }
        }

        if (existing is { FilterPresetID: > 0 } && filter.IsDirectory != existing.IsDirectory)
            AddError(nameof(filter.IsDirectory), "A filter can not become a directory filter, or stop being one, after it is created.");

        if (filter.IsDirectory)
        {
            if (filter.Expression != null)
                AddError(nameof(filter.Expression), "Directory filters cannot have any conditions applied to them.");

            if (filter.Sorting != null)
                AddError(nameof(filter.Sorting), "Directory filters cannot have custom sorting applied to them.");
        }

        return isValid;
    }
}
