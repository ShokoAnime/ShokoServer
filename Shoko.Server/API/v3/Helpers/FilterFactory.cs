using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Helpers;

public class FilterFactory
{
    private readonly HttpContext _context;
    private readonly FilterEvaluator _evaluator;

    public FilterFactory(HttpContext context, FilterEvaluator evaluator)
    {
        _context = context;
        _evaluator = evaluator;
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

    public static Filter.FilterCondition GetExpressionTree(FilterExpression expression)
    {
        if (expression is null) return null;
        var result = new Filter.FilterCondition
        {
            Type = expression.GetType().Name.Replace("Expression", "")
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
        }

        // Parameters
        switch (expression)
        {
            case IWithStringParameter parameter:
                result.Parameter = parameter.Parameter;
                break;
            case IWithNumberParameter parameter:
                result.Parameter = parameter.Parameter.ToString();
                break;
            case IWithDateParameter parameter:
                result.Parameter = parameter.Parameter.ToString("yyyy-MM-dd");
                break;
            case IWithTimeSpanParameter parameter:
                result.Parameter = parameter.Parameter.ToString("G");
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

    public static Filter.SortingCriteria GetSortingCriteria(SortingExpression expression)
    {
        var result = new Filter.SortingCriteria
        {
            Type = expression.GetType().Name.Replace("Selector", ""),
            IsInverted = expression.Descending
        };

        var currentCriteria = result;
        var currentExpression = expression;
        while (currentExpression.Next != null)
        {
            currentCriteria.Next = new Filter.SortingCriteria
            {
                Type = currentExpression.GetType().Name.Replace("Selector", ""),
                IsInverted = currentExpression.Descending
            };
            currentCriteria = currentCriteria.Next;
            currentExpression = currentExpression.Next;
        }

        return result;
    }
    
    public Filter.Input.CreateOrUpdateFilterBody CreateOrUpdateFilterBody(FilterPreset groupFilter)
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

    public Filter MergeWithExisting(Filter.Input.CreateOrUpdateFilterBody body, FilterPreset groupFilter, ModelStateDictionary modelState, bool skipSave = false)
    {
        if (groupFilter.Locked)
            modelState.AddModelError("IsLocked", "Filter is locked.");

        // Defer to `null` if the id is `0`.
        if (body.ParentID is 0)
            body.ParentID = null;

        if (body.ParentID.HasValue)
        {
            var parentFilter = RepoFactory.FilterPreset.GetByID(body.ParentID.Value);
            if (parentFilter == null)
            {
                modelState.AddModelError(nameof(body.ParentID), $"Unable to find parent filter with id {body.ParentID.Value}");
            }
            else
            {
                if (parentFilter.Locked)
                    modelState.AddModelError(nameof(body.ParentID), $"Unable to add a sub-filter to a filter that is locked.");

                if (!parentFilter.IsDirectory())
                    modelState.AddModelError(nameof(body.ParentID), $"Unable to add a sub-filter to a filter that is not a directorty filter.");
            }
        }

        if (body.IsDirectory)
        {
            if (body.Expression != null)
                modelState.AddModelError(nameof(body.Expression), "Directory filters cannot have any conditions applied to them.");

            if (body.Sorting != null)
                modelState.AddModelError(nameof(body.Sorting), "Directory filters cannot have custom sorting applied to them.");
        }
        else
        {
            var subFilters = groupFilter.FilterPresetID != 0 ? RepoFactory.FilterPreset.GetByParentID(groupFilter.FilterPresetID) : new();
            if (subFilters.Count > 0)
                modelState.AddModelError(nameof(body.IsDirectory), "Cannot turn a directory filter with sub-filters into a normal filter without first removing the sub-filters");
        }

        // Return now if we encountered any validation errors.
        if (!modelState.IsValid)
            return null;

        groupFilter.ParentFilterPresetID = body.ParentID;
        groupFilter.FilterType = body.IsDirectory ? GroupFilterType.UserDefined | GroupFilterType.Directory : GroupFilterType.UserDefined;
        groupFilter.Name = body.Name;
        groupFilter.Hidden = body.IsHidden;
        groupFilter.ApplyAtSeriesLevel = body.ApplyAtSeriesLevel;
        if (!body.IsDirectory)
        {
            if (body.Expression != null)
            {
                // TODO convert back from API model
                //groupFilter.Expression = body.Expression
            }

            if (body.Sorting != null)
            {
                // TODO Convert back from API model
            }
        }

        // Skip saving if we're just going to preview a group filter.
        if (!skipSave)
            RepoFactory.FilterPreset.Save(groupFilter);

        return GetFilter(groupFilter, true);
    }

    public Filter GetFirstAiringSeasonGroupFilter(SVR_AniDB_Anime anime)
    {
        var type = (AnimeType)anime.AnimeType;
        if (type != AnimeType.TVSeries && type != AnimeType.Web)
            return null;

        var (year, season) = anime.GetSeasons()
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
