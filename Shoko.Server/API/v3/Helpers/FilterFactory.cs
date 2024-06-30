using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Helpers;

public class FilterFactory
{
    private readonly Dictionary<string, Type> _expressionTypes;
    private readonly Dictionary<string, Type> _sortingTypes;
    private readonly HttpContext _context;
    private readonly FilterEvaluator _evaluator;

    public FilterFactory(IHttpContextAccessor context, FilterEvaluator evaluator)
    {
        _context = context.HttpContext;
        _evaluator = evaluator;

        _expressionTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => a != typeof(FilterExpression) && !a.IsGenericType && typeof(FilterExpression).IsAssignableFrom(a) &&
                        !typeof(SortingExpression).IsAssignableFrom(a)).ToDictionary(a => a.Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector"));

        _sortingTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => a != typeof(FilterExpression) && !a.IsAbstract && !a.IsGenericType && typeof(SortingExpression).IsAssignableFrom(a))
            .ToDictionary(a => a.Name.TrimEnd("SortingSelector"));
    }

    public Filter GetFilter(FilterPreset groupFilter, bool fullModel = false)
    {
        var user = _context.GetUser();
        var filter = new Filter
        {
            IDs = new Filter.FilterIDs
            {
                ID = groupFilter.FilterPresetID, ParentFilter = groupFilter.ParentFilterPresetID
            },
            Name = groupFilter.Name,
            IsLocked = groupFilter.Locked,
            IsDirectory = groupFilter.IsDirectory(),
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
            : _evaluator.EvaluateFilter(groupFilter, user?.JMMUserID).Count();
        return filter;
    }

    public IEnumerable<Filter> GetFilters(List<FilterPreset> groupFilters, bool fullModel = false)
    {
        var user = _context.GetUser();
        var evaluate = groupFilters.Any(a => !a.IsDirectory());
        var results = evaluate ? _evaluator.BatchEvaluateFilters(groupFilters, user.JMMUserID, true) : null;
        var filters = groupFilters.Select(groupFilter =>
        {
            var filter = new Filter
            {
                IDs = new Filter.FilterIDs
                {
                    ID = groupFilter.FilterPresetID, ParentFilter = groupFilter.ParentFilterPresetID
                },
                Name = groupFilter.Name,
                IsLocked = groupFilter.Locked,
                IsDirectory = groupFilter.IsDirectory(),
                IsHidden = groupFilter.Hidden,
                ApplyAtSeriesLevel = groupFilter.ApplyAtSeriesLevel,
            };

            if (fullModel)
            {
                filter.Expression = GetExpressionTree(groupFilter.Expression);
                filter.Sorting = GetSortingCriteria(groupFilter.SortingExpression);
            }

            filter.Size = filter.IsDirectory ? RepoFactory.FilterPreset.GetByParentID(groupFilter.FilterPresetID).Count : results?[groupFilter].Count() ?? 0;
            return filter;
        });

        return filters;
    }

    public Filter.FilterCondition GetExpressionTree(FilterExpression expression)
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
            case IWithStringParameter parameter:
                result.Parameter = parameter.Parameter;
                break;
            case IWithNumberParameter parameter:
                result.Parameter = parameter.Parameter == 0 ? null : parameter.Parameter.ToString(CultureInfo.CurrentCulture);
                break;
            case IWithDateParameter parameter:
                result.Parameter = parameter.Parameter == default ? null : parameter.Parameter.ToString("yyyy-MM-dd");
                break;
            case IWithTimeSpanParameter parameter:
                result.Parameter = parameter.Parameter == default ? null : parameter.Parameter.ToString("G");
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
            case IWithSecondStringParameter right:
                result.SecondParameter = right.SecondParameter;
                break;
        }

        return result;
    }
    
    public FilterExpression<T> GetExpressionTree<T>(Filter.FilterCondition condition)
    {
        if (condition is null) return null;
        if (!_expressionTypes.TryGetValue(condition.Type.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").Trim(), out var type))
            throw new ArgumentException($"FilterCondition type {condition.Type} was not found");
        var result = (FilterExpression<T>)Activator.CreateInstance(type);

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
            case IWithStringParameter parameter:
                parameter.Parameter = condition.Parameter;
                break;
            case IWithNumberParameter parameter:
                parameter.Parameter = string.IsNullOrEmpty(condition.Parameter) ? default : double.Parse(condition.Parameter!);
                break;
            case IWithDateParameter parameter:
                parameter.Parameter = string.IsNullOrEmpty(condition.Parameter)
                    ? default
                    : DateTime.ParseExact(condition.Parameter!, "yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat);
                break;
            case IWithTimeSpanParameter parameter:
                parameter.Parameter = string.IsNullOrEmpty(condition.Parameter)
                    ? default
                    : TimeSpan.ParseExact(condition.Parameter!, "G", CultureInfo.InvariantCulture.DateTimeFormat);
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
            case IWithSecondStringParameter right:
                right.SecondParameter = condition.SecondParameter;
                break;
        }

        return result;
    }

    public Filter.SortingCriteria GetSortingCriteria(SortingExpression expression)
    {
        if (expression == null) return new Filter.SortingCriteria { Type = "Name", IsInverted = false };

        var result = new Filter.SortingCriteria
        {
            Type = expression.GetType().Name.TrimEnd("SortingSelector"),
            IsInverted = expression.Descending
        };

        var currentCriteria = result;
        var currentExpression = expression;
        while (currentExpression.Next != null)
        {
            currentCriteria.Next = new Filter.SortingCriteria
            {
                Type = currentExpression.GetType().Name.TrimEnd("SortingSelector"),
                IsInverted = currentExpression.Descending
            };
            currentCriteria = currentCriteria.Next;
            currentExpression = currentExpression.Next;
        }

        return result;
    }
    
    public SortingExpression GetSortingCriteria(Filter.SortingCriteria criteria)
    {
        if (!_sortingTypes.TryGetValue(criteria.Type, out var type))
            throw new ArgumentException($"SortingExpression type {criteria.Type}Selector was not found");
        var result = (SortingExpression)Activator.CreateInstance(type)!;
        result.Descending = criteria.IsInverted;

        if (criteria.Next != null) result.Next = GetSortingCriteria(criteria.Next);

        return result;
    }
    
    public Filter.Input.CreateOrUpdateFilterBody GetPostModel(FilterPreset groupFilter)
    {
        var result = new Filter.Input.CreateOrUpdateFilterBody
        {
            Name = groupFilter.Name,
            ParentID = groupFilter.ParentFilterPresetID,
            IsDirectory = groupFilter.IsDirectory(),
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

    public FilterPreset GetFilterPreset(Filter.Input.CreateOrUpdateFilterBody filter, ModelStateDictionary modelState = null, FilterPreset existing = null)
    {
        existing ??= new FilterPreset();

        if (existing.Locked)
            modelState?.AddModelError("IsLocked", "Filter is locked.");

        // Defer to `null` if the id is `0`.
        if (filter.ParentID is 0)
            filter.ParentID = null;

        if (filter.ParentID.HasValue)
        {
            var parentFilter = RepoFactory.FilterPreset.GetByID(filter.ParentID.Value);
            if (parentFilter == null)
            {
                modelState?.AddModelError(nameof(filter.ParentID), $"Unable to find parent filter with id {filter.ParentID.Value}");
            }
            else
            {
                if (parentFilter.Locked)
                    modelState?.AddModelError(nameof(filter.ParentID), $"Unable to add a sub-filter to a filter that is locked.");

                if (!parentFilter.IsDirectory())
                    modelState?.AddModelError(nameof(filter.ParentID), $"Unable to add a sub-filter to a filter that is not a directorty filter.");
            }
        }

        if (filter.IsDirectory)
        {
            if (filter.Expression != null)
                modelState?.AddModelError(nameof(filter.Expression), "Directory filters cannot have any conditions applied to them.");

            if (filter.Sorting != null)
                modelState?.AddModelError(nameof(filter.Sorting), "Directory filters cannot have custom sorting applied to them.");
        }
        else
        {
            var subFilters = existing.FilterPresetID != 0 ? RepoFactory.FilterPreset.GetByParentID(existing.FilterPresetID) : new();
            if (subFilters.Count > 0)
                modelState?.AddModelError(nameof(filter.IsDirectory), "Cannot turn a directory filter with sub-filters into a normal filter without first removing the sub-filters");
        }

        // Return now if we encountered any validation errors.
        if (modelState is { IsValid: false })
            return null;

        existing.ParentFilterPresetID = filter.ParentID;
        existing.FilterType = filter.IsDirectory ? GroupFilterType.UserDefined | GroupFilterType.Directory : GroupFilterType.UserDefined;
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

    public Filter GetFirstAiringSeasonGroupFilter(SVR_AniDB_Anime anime)
    {
        var type = (AnimeType)anime.AnimeType;
        if (type != AnimeType.TVSeries && type != AnimeType.Web)
            return null;

        var (year, season) = anime.Seasons
            .FirstOrDefault();
        if (year == 0)
            return null;

        var seasonName = $"{season} {year}";
        var seasonsFilterID = RepoFactory.FilterPreset.GetTopLevel()
            .FirstOrDefault(f => f.FilterType == (GroupFilterType.Directory | GroupFilterType.Season))?.FilterPresetID;
        if (seasonsFilterID == null) return null;
        var firstAirSeason = RepoFactory.FilterPreset.GetByParentID(seasonsFilterID.Value)
            .FirstOrDefault(f => f.Name == seasonName);
        if (firstAirSeason == null)
            return null;

        return GetFilter(firstAirSeason);
    }
}
