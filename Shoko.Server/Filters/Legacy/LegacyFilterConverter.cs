using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Legacy;

public class LegacyFilterConverter
{
    private readonly FilterEvaluator _evaluator;

    public LegacyFilterConverter(FilterEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public FilterPreset FromClient(CL_GroupFilter model)
    {
        var expression = LegacyConditionConverter.GetExpression(model.FilterConditions, (GroupFilterBaseCondition)model.BaseCondition);
        var filter = new FilterPreset
        {
            FilterPresetID = model.GroupFilterID,
            ParentFilterPresetID = model.ParentGroupFilterID,
            Name = model.GroupFilterName,
            FilterType = (GroupFilterType)model.FilterType,
            Hidden = model.InvisibleInClients == 1,
            Locked = model.Locked == 1,
            ApplyAtSeriesLevel = model.ApplyToSeries == 1,
            SortingExpression = LegacyConditionConverter.GetSortingExpression(model.SortingCriteria),
            Expression = expression
        };
        return filter;
    }

    public FilterPreset FromLegacy(GroupFilter model, List<GroupFilterCondition> conditions)
    {
        var expression = LegacyConditionConverter.GetExpression(conditions, (GroupFilterBaseCondition)model.BaseCondition);
        var filter = new FilterPreset
        {
            Name = model.GroupFilterName,
            FilterType = (GroupFilterType)model.FilterType,
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
            foreach (var userID in RepoFactory.JMMUser.GetAll().Select(a => a.JMMUserID))
            {
                var results = _evaluator.EvaluateFilter(filter, userID).ToList();
                groupIds[userID] = results.Select(a => a.Key).ToHashSet();
                seriesIds[userID] = results.SelectMany(a => a).ToHashSet();
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
        foreach (var condition in conditions) condition.GroupFilterID = filter.FilterPresetID;
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
    
    public Dictionary<FilterPreset, CL_GroupFilter> ToClient(IReadOnlyList<FilterPreset> filters)
    {
        var result = new Dictionary<FilterPreset, CL_GroupFilter>();
        var userFilters = filters.Where(a => (a?.Expression?.UserDependent ?? false) || (a?.SortingExpression?.UserDependent ?? false)).ToList();
        var otherFilters = filters.Except(userFilters).ToList();

        // batch evaluate each list, then build the mappings
        var userResults = RepoFactory.JMMUser.GetAll()
            .SelectMany(user => _evaluator.BatchEvaluateFilters(userFilters, user.JMMUserID).Select(a => (a.Key, user.JMMUserID, a.Value)))
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
            foreach (var condition in conditions) condition.GroupFilterID = filter.FilterPresetID;
            return (Filter: filter, new CL_GroupFilter
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
            });
        });

        foreach (var (filter, model) in userModels)
        {
            result[filter] = model;
        }

        if (otherFilters.Count > 0)
        {
            var results = _evaluator.BatchEvaluateFilters(otherFilters, null);
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
                foreach (var condition in conditions) condition.GroupFilterID = filter.FilterPresetID;
                return (Filter: filter, new CL_GroupFilter
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
                });
            });

            foreach (var (filter, model) in models)
            {
                result[filter] = model;
            }
        }

        return result;
    }
}
