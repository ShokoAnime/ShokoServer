using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Filters.Legacy;

public class LegacyFilterConverter
{
    private readonly IFilterEvaluator _evaluator;

    public LegacyFilterConverter(IFilterEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public FilterPreset FromClient(CL_GroupFilter model)
    {
        var expression = LegacyConditionConverter.GetExpression(model.FilterConditions, (CL_GroupFilterBaseCondition)model.BaseCondition);
        var filter = new FilterPreset
        {
            FilterPresetID = model.GroupFilterID,
            ParentFilterPresetID = model.ParentGroupFilterID,
            Name = model.GroupFilterName,
            FilterType = (FilterPresetType)model.FilterType,
            Hidden = model.InvisibleInClients == 1,
            Locked = model.Locked == 1,
            ApplyAtSeriesLevel = model.ApplyToSeries == 1,
            SortingExpression = LegacyConditionConverter.GetSortingExpression(model.SortingCriteria),
            Expression = expression
        };
        return filter;
    }

    public FilterPreset FromLegacy(CL_GroupFilter model, List<CL_GroupFilterCondition> conditions)
    {
        var expression = LegacyConditionConverter.GetExpression(conditions, (CL_GroupFilterBaseCondition)model.BaseCondition);
        var filter = new FilterPreset
        {
            Name = model.GroupFilterName,
            FilterType = (FilterPresetType)model.FilterType,
            Hidden = model.InvisibleInClients == 1,
            Locked = model.Locked == 1,
            ApplyAtSeriesLevel = model.ApplyToSeries == 1,
            SortingExpression = LegacyConditionConverter.GetSortingExpression(model.SortingCriteria),
            Expression = expression
        };
        return filter;
    }

    public CL_GroupFilter ToClient(FilterPreset filter)
    {
        if (filter == null) return null;
        var groupIds = new Dictionary<int, HashSet<int>>();
        var seriesIds = new Dictionary<int, HashSet<int>>();
        if ((filter.Expression?.UserDependent ?? false) || (filter.SortingExpression?.UserDependent ?? false))
        {
            foreach (var user in RepoFactory.JMMUser.GetAll())
            {
                var results = _evaluator.EvaluateFilter(filter, user).ToList();
                groupIds[user.JMMUserID] = results.Select(a => a.Key).ToHashSet();
                seriesIds[user.JMMUserID] = results.SelectMany(a => a).ToHashSet();
            }
        }
        else
        {
            var results = _evaluator.EvaluateFilter(filter, null).ToList();
            var groupIdSet = results.Select(a => a.Key).ToHashSet();
            var seriesIdSet = results.SelectMany(a => a).ToHashSet();
            foreach (var userID in RepoFactory.JMMUser.GetAll().Select(a => a.JMMUserID))
            {
                groupIds[userID] = groupIdSet;
                seriesIds[userID] = seriesIdSet;
            }
        }

        LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        conditions?.ForEach(condition => condition.GroupFilterID = filter.FilterPresetID);
        var contract = new CL_GroupFilter
        {
            GroupFilterID = filter.FilterPresetID,
            GroupFilterName = filter.Name,
            ApplyToSeries = filter.ApplyAtSeriesLevel ? 1 : 0,
            Locked = filter.Locked ? 1 : 0,
            FilterType = (int)filter.FilterType,
            ParentGroupFilterID = filter.ParentFilterPresetID,
            InvisibleInClients = filter.Hidden ? 1 : 0,
            BaseCondition = (int)baseCondition,
            FilterConditions = conditions,
            SortingCriteria = LegacyConditionConverter.GetSortingCriteria(filter),
            Groups = groupIds,
            Series = seriesIds,
            Childs = filter.FilterPresetID == 0
                ? new HashSet<int>()
                : RepoFactory.FilterPreset.GetByParentID(filter.FilterPresetID).Select(a => a.FilterPresetID).ToHashSet()
        };
        return contract;
    }

    public List<CL_GroupFilter> ToClient(IReadOnlyList<FilterPreset> filters, int? userID = null)
    {
        var result = new List<CL_GroupFilter>();
        var userFilters = filters.Where(a => a?.Expression?.UserDependent ?? false).ToList();
        var otherFilters = filters.Except(userFilters).ToList();

        // batch evaluate each list, then build the mappings
        if (userFilters.Count > 0) result.AddRange(SetUserFilters(userID, userFilters));
        if (otherFilters.Count > 0) result.AddRange(SetOtherFilters(otherFilters));

        return result;
    }

    private List<CL_GroupFilter> SetOtherFilters(List<FilterPreset> otherFilters)
    {
        var results = _evaluator.BatchPrepareFilters(otherFilters, null, skipSorting: true);
        var models = results.Select(kv =>
        {
            var filter = kv.Key;
            var groupIds = new Dictionary<int, HashSet<int>>();
            var seriesIds = new Dictionary<int, HashSet<int>>();
            var groupIdSet = kv.Value.Select(a => a.Key).ToHashSet();
            var seriesIdSet = kv.Value.SelectMany(a => a).ToHashSet();
            foreach (var user in RepoFactory.JMMUser.GetAll())
            {
                groupIds[user.JMMUserID] = groupIdSet.Where(a => user.AllowedGroup(RepoFactory.AnimeGroup.GetByID(a))).ToHashSet();
                seriesIds[user.JMMUserID] = seriesIdSet.Where(a => user.AllowedSeries(RepoFactory.AnimeSeries.GetByID(a))).ToHashSet();
            }

            LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
            conditions?.ForEach(condition => condition.GroupFilterID = filter.FilterPresetID);
            return new CL_GroupFilter
            {
                GroupFilterID = filter.FilterPresetID,
                GroupFilterName = filter.Name,
                ApplyToSeries = filter.ApplyAtSeriesLevel ? 1 : 0,
                Locked = filter.Locked ? 1 : 0,
                FilterType = (int)filter.FilterType,
                ParentGroupFilterID = filter.ParentFilterPresetID,
                InvisibleInClients = filter.Hidden ? 1 : 0,
                BaseCondition = (int)baseCondition,
                FilterConditions = conditions,
                SortingCriteria = LegacyConditionConverter.GetSortingCriteria(filter),
                Groups = groupIds,
                Series = seriesIds,
                Childs = filter.FilterPresetID == 0
                    ? new HashSet<int>()
                    : RepoFactory.FilterPreset.GetByParentID(filter.FilterPresetID).Select(a => a.FilterPresetID).ToHashSet()
            };
        }).ToList();

        return models;
    }

    /// <summary>
    /// Batch Evaluates filters and sets the models
    /// </summary>
    /// <param name="userID">if this is specified, it only calculates one user</param>
    /// <param name="userFilters"></param>
    private List<CL_GroupFilter> SetUserFilters(int? userID, List<FilterPreset> userFilters)
    {
        var userResults = userID.HasValue
            ? _evaluator.BatchPrepareFilters(userFilters, RepoFactory.JMMUser.GetByID(userID.Value), skipSorting: true)
                .Select(a => (a.Key, JMMUserID: userID.Value, a.Value))
                .GroupBy(a => a.Key, a => (a.JMMUserID, a.Value))
            : RepoFactory.JMMUser.GetAll()
                .SelectMany(user => _evaluator.BatchPrepareFilters(userFilters, user, skipSorting: true).Select(a => (a.Key, user.JMMUserID, a.Value)))
                .GroupBy(a => a.Key, a => (a.JMMUserID, a.Value));
        var userModels = userResults.Select(group =>
        {
            var filter = group.Key;
            var groupIds = new Dictionary<int, HashSet<int>>();
            var seriesIds = new Dictionary<int, HashSet<int>>();
            foreach (var kv in group)
            {
                groupIds[kv.JMMUserID] = kv.Value.Select(a => a.Key).ToHashSet();
                seriesIds[kv.JMMUserID] = kv.Value.SelectMany(a => a).ToHashSet();
            }
            LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
            conditions?.ForEach(condition => condition.GroupFilterID = filter.FilterPresetID);
            return new CL_GroupFilter
            {
                GroupFilterID = filter.FilterPresetID,
                GroupFilterName = filter.Name,
                ApplyToSeries = filter.ApplyAtSeriesLevel ? 1 : 0,
                Locked = filter.Locked ? 1 : 0,
                FilterType = (int)filter.FilterType,
                ParentGroupFilterID = filter.ParentFilterPresetID,
                InvisibleInClients = filter.Hidden ? 1 : 0,
                BaseCondition = (int)baseCondition,
                FilterConditions = conditions,
                SortingCriteria = LegacyConditionConverter.GetSortingCriteria(filter),
                Groups = groupIds,
                Series = seriesIds,
                Childs = filter.FilterPresetID == 0
                    ? new HashSet<int>()
                    : RepoFactory.FilterPreset.GetByParentID(filter.FilterPresetID).Select(a => a.FilterPresetID).ToHashSet()
            };
        }).ToList();

        return userModels;
    }
}
