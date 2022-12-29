using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using FluentNHibernate.MappingModel;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Models;

public class SVR_GroupFilter : GroupFilter
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    public int GroupsIdsVersion { get; set; }
    public string GroupsIdsString { get; set; }

    public int GroupConditionsVersion { get; set; }
    public string GroupConditions { get; set; }

    public int SeriesIdsVersion { get; set; }
    public string SeriesIdsString { get; set; }

    public const int GROUPFILTER_VERSION = 3;
    public const int GROUPCONDITIONS_VERSION = 1;
    public const int SERIEFILTER_VERSION = 2;


    internal Dictionary<int, HashSet<int>> _groupsId = new();
    internal Dictionary<int, HashSet<int>> _seriesId = new();
    internal List<GroupFilterCondition> _conditions = new();

    public SVR_GroupFilter Parent =>
        ParentGroupFilterID.HasValue ? RepoFactory.GroupFilter.GetByID(ParentGroupFilterID.Value) : null;

    public SVR_GroupFilter TopLevelGroupFilter
    {
        get
        {
            var parent = Parent;
            if (parent == null)
            {
                return this;
            }

            while (true)
            {
                var next = parent.Parent;
                if (next == null)
                {
                    return parent;
                }

                parent = next;
            }
        }
    }


    public virtual HashSet<GroupFilterConditionType> Types =>
        new HashSet<GroupFilterConditionType>(
            Conditions.Select(a => a.ConditionType).Cast<GroupFilterConditionType>());

    public virtual Dictionary<int, HashSet<int>> GroupsIds
    {
        get
        {
            if (_groupsId.Count != 0 || GroupsIdsVersion != GROUPFILTER_VERSION)
            {
                return _groupsId;
            }

            var vals = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(GroupsIdsString);
            if (vals == null)
            {
                return _groupsId;
            }

            _groupsId = vals.ToDictionary(a => a.Key, a => new HashSet<int>(a.Value));
            return _groupsId;
        }
        set => _groupsId = value;
    }

    public virtual Dictionary<int, HashSet<int>> SeriesIds
    {
        get
        {
            if (_seriesId.Count != 0 || SeriesIdsVersion != SERIEFILTER_VERSION)
            {
                return _seriesId;
            }

            var vals = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(SeriesIdsString);
            if (vals == null)
            {
                return _seriesId;
            }

            _seriesId = vals.ToDictionary(a => a.Key, a => new HashSet<int>(a.Value));

            return _seriesId;
        }
        set => _seriesId = value;
    }

    public virtual List<GroupFilterCondition> Conditions
    {
        get
        {
            if (_conditions.Count == 0 && !string.IsNullOrEmpty(GroupConditions))
            {
                _conditions = JsonConvert.DeserializeObject<List<GroupFilterCondition>>(GroupConditions);
            }

            return _conditions;
        }
        set
        {
            if (value != null)
            {
                _conditions = value;
                GroupConditions = JsonConvert.SerializeObject(_conditions);
            }
        }
    }

    public override string ToString()
    {
        return $"{GroupFilterID} - {GroupFilterName}";
    }

    public List<GroupFilterSortingCriteria> SortCriteriaList
    {
        get
        {
            var sortCriteriaList = new List<GroupFilterSortingCriteria>();

            if (!string.IsNullOrEmpty(SortingCriteria))
            {
                var scrit = SortingCriteria.Split('|');
                foreach (var sortpair in scrit)
                {
                    var spair = sortpair.Split(';');
                    if (spair.Length != 2)
                    {
                        continue;
                    }

                    int.TryParse(spair[0], out var stype);
                    int.TryParse(spair[1], out var sdir);

                    if (stype > 0 && sdir > 0)
                    {
                        var gfsc = new GroupFilterSortingCriteria
                        {
                            GroupFilterID = GroupFilterID,
                            SortType = (GroupFilterSorting)stype,
                            SortDirection = (GroupFilterSortDirection)sdir
                        };
                        sortCriteriaList.Add(gfsc);
                    }
                }
            }

            return sortCriteriaList;
        }

        set
        {
            var crts = value.Select(a => $"{(int)a.SortType};{(int)a.SortDirection}");
            SortingCriteria = string.Join("|", crts);
        }
    }

    public bool DeleteGroupFromFilters(int userID, int groupID)
    {
        return WriteLock(
            () =>
            {
                if (!GroupsIds.ContainsKey(userID))
                {
                    return false;
                }

                if (!GroupsIds[userID].Contains(groupID))
                {
                    return false;
                }

                GroupsIds[userID].Remove(groupID);
                return true;
            }
        );
    }

    public bool RemoveUser(int userID)
    {
        var changed = WriteLock(
            () =>
            {
                var changed = false;
                if (GroupsIds.ContainsKey(userID))
                {
                    GroupsIds.Remove(userID);
                    changed = true;
                }

                if (SeriesIds.ContainsKey(userID))
                {
                    SeriesIds.Remove(userID);
                    changed = true;
                }

                return changed;
            }
        );

        if (!changed)
        {
            return false;
        }

        UpdateEntityReferenceStrings();
        return true;
    }

    public CL_GroupFilter ToClient()
    {
        if (Conditions.FirstOrDefault(a => a.GroupFilterID == 0) != null)
        {
            Conditions.ForEach(a => a.GroupFilterID = GroupFilterID);
            RepoFactory.GroupFilter.Save(this);
        }

        var contract = new CL_GroupFilter
        {
            GroupFilterID = GroupFilterID,
            GroupFilterName = GroupFilterName,
            ApplyToSeries = ApplyToSeries,
            BaseCondition = BaseCondition,
            SortingCriteria = SortingCriteria,
            Locked = Locked,
            FilterType = FilterType,
            ParentGroupFilterID = ParentGroupFilterID,
            InvisibleInClients = InvisibleInClients,
            FilterConditions = Conditions,
            Groups = GroupsIds,
            Series = SeriesIds,
            Childs = GroupFilterID == 0
                ? new HashSet<int>()
                : RepoFactory.GroupFilter.GetByParentID(GroupFilterID).Select(a => a.GroupFilterID).ToHashSet()
        };
        return contract;
    }

    public static SVR_GroupFilter FromClient(CL_GroupFilter gfc)
    {
        var gf = new SVR_GroupFilter
        {
            GroupFilterID = gfc.GroupFilterID,
            GroupFilterName = gfc.GroupFilterName,
            ApplyToSeries = gfc.ApplyToSeries,
            BaseCondition = gfc.BaseCondition,
            SortingCriteria = gfc.SortingCriteria,
            Locked = gfc.Locked,
            InvisibleInClients = gfc.InvisibleInClients,
            ParentGroupFilterID = gfc.ParentGroupFilterID,
            FilterType = gfc.FilterType,
            Conditions = gfc.FilterConditions,
            GroupsIds = gfc.Groups ?? new Dictionary<int, HashSet<int>>(),
            SeriesIds = gfc.Series ?? new Dictionary<int, HashSet<int>>()
        };
        if (gf.GroupFilterID != 0)
        {
            gf.Conditions.ForEach(a => a.GroupFilterID = gf.GroupFilterID);
        }

        return gf;
    }

    public CL_GroupFilterExtended ToClientExtended(SVR_JMMUser user)
    {
        var contract = new CL_GroupFilterExtended { GroupFilter = ToClient(), GroupCount = 0, SeriesCount = 0 };

        ReadLock(
            () =>
            {
                if (GroupsIds.ContainsKey(user.JMMUserID))
                {
                    contract.GroupCount = GroupsIds[user.JMMUserID].Count;
                }

                if (SeriesIds.ContainsKey(user.JMMUserID))
                {
                    contract.SeriesCount = SeriesIds[user.JMMUserID].Count;
                }
            }
        );

        return contract;
    }

    public bool UpdateGroupFilterFromSeries(CL_AnimeSeries_User ser, JMMUser user)
    {
        if (ser == null)
        {
            return false;
        }

        bool result;
        if (ApplyToSeries == 1)
        {
            result = CalculateGroupFilterSeries(RepoFactory.AnimeSeries.GetAll().Select(a => a.AnimeSeriesID).ToHashSet(), ser, user);
            if (!result)
            {
                return false;
            }

            WriteLock(
                () =>
                {
                    GroupsIds[user?.JMMUserID ?? 0] = SeriesIds[user?.JMMUserID ?? 0]
                        .Select(a => RepoFactory.AnimeSeries.GetByID(a)?.TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                        .Where(a => a != -1).ToHashSet();
                }
            );
        }
        else
        {
            result = false;
            // Top Level Group
            int? groupID = ser.AnimeGroupID;
            while (true)
            {
                if (groupID == null)
                {
                    break;
                }

                var grp = RepoFactory.AnimeGroup.GetByID(groupID.Value);
                if (grp != null)
                {
                    groupID = grp.AnimeGroupParentID;
                }
                else
                {
                    break;
                }
            }

            if (groupID == null)
            {
                return false;
            }

            var group = RepoFactory.AnimeGroup.GetByID(groupID.Value);

            var contract = group?.Contract;
            if (user != null)
            {
                contract = group?.GetUserContract(user.JMMUserID);
            }

            if (contract == null)
            {
                return false;
            }

            result |= CalculateGroupFilterGroups(RepoFactory.AnimeGroup.GetAll().Select(a => a.AnimeGroupID).ToHashSet(), contract, user);
            if (!result)
            {
                return false;
            }

            WriteLock(
                () =>
                {
                    SeriesIds[user?.JMMUserID ?? 0] = GroupsIds[user?.JMMUserID ?? 0]
                        .SelectMany(
                            a =>
                                RepoFactory.AnimeGroup.GetByID(a)?.GetAllSeries()?.Select(b => b?.AnimeSeriesID ?? -1)
                        )
                        .Where(a => a != -1).ToHashSet();
                }
            );
        }

        return true;
    }

    public bool UpdateGroupFilterFromGroup(CL_AnimeGroup_User grp, JMMUser user)
    {
        if (grp == null)
        {
            return false;
        }

        bool result;
        if (ApplyToSeries == 1)
        {
            result = false;
            var sers = new List<SVR_AnimeSeries>();
            SVR_AnimeGroup.GetAnimeSeriesRecursive(grp.AnimeGroupID, ref sers);

            foreach (var ser in sers)
            {
                var contract = ser.Contract;
                if (user != null)
                {
                    contract = ser.GetUserContract(user.JMMUserID);
                }

                result |= CalculateGroupFilterSeries(RepoFactory.AnimeSeries.GetAll().Select(a => a.AnimeSeriesID).ToHashSet(), contract, user);
            }

            if (!result)
            {
                return false;
            }

            WriteLock(
                () =>
                {
                    GroupsIds[user?.JMMUserID ?? 0] = SeriesIds[user?.JMMUserID ?? 0]
                        .Select(a => RepoFactory.AnimeSeries.GetByID(a)?.TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                        .Where(a => a != -1).ToHashSet();
                }
            );
        }
        else
        {
            result = CalculateGroupFilterGroups(RepoFactory.AnimeGroup.GetAll().Select(a => a.AnimeGroupID).ToHashSet(), grp, user);
            if (!result)
            {
                return false;
            }

            WriteLock(
                () =>
                {
                    SeriesIds[user?.JMMUserID ?? 0] = GroupsIds[user?.JMMUserID ?? 0].SelectMany(
                            a => RepoFactory.AnimeGroup.GetByID(a)
                                ?.GetAllSeries()
                                ?.Select(b => b?.AnimeSeriesID ?? -1)
                        )
                        .Where(a => a != -1)
                        .ToHashSet();
                }
            );
        }

        return true;
    }


    private bool CalculateGroupFilterSeries(HashSet<int> allSeriesIds, CL_AnimeSeries_User ser, JMMUser user)
    {
        if (ser == null)
        {
            return false;
        }

        var seriesIds = ReadLock(() => SeriesIds.TryGetValue(user?.JMMUserID ?? 0, out var seriesIds) ? seriesIds : null);

        var change = false;
        if (seriesIds == null)
            seriesIds = new HashSet<int>();
        else
            change = seriesIds.RemoveWhere(a => !allSeriesIds.Contains(a)) > 0;

        if (EvaluateGroupFilter(ser, user))
        {
            change |= seriesIds.Add(ser.AnimeSeriesID);
        }
        else
        {
            change |= seriesIds.Remove(ser.AnimeSeriesID);
        }

        WriteLock(() => SeriesIds[user?.JMMUserID ?? 0] = seriesIds);

        return change;
    }

    private bool CalculateGroupFilterGroups(HashSet<int> allGroupIds, CL_AnimeGroup_User grp, JMMUser user)
    {
        if (grp == null) return false;

        var groupIds = ReadLock(() =>
            GroupsIds.TryGetValue(user?.JMMUserID ?? 0, out var groupIds) ? new HashSet<int>(groupIds) : null);

        var change = false;
        if (groupIds == null)
            groupIds = new HashSet<int>();
        else
            change = groupIds.RemoveWhere(a => !allGroupIds.Contains(a)) > 0;

        if (EvaluateGroupFilter(grp, user))
        {
            change |= groupIds.Add(grp.AnimeGroupID);
        }
        else
        {
            change |= groupIds.Remove(grp.AnimeGroupID);
        }

        WriteLock(() => GroupsIds[user?.JMMUserID ?? 0] = groupIds);

        return change;
    }

    public void CalculateGroupsAndSeries()
    {
        if (ApplyToSeries == 1)
        {
            EvaluateAnimeSeries();

            var erroredSeries = new HashSet<int>();
            WriteLock(
                () =>
                {
                    foreach (var user in SeriesIds.Keys)
                    {
                        GroupsIds[user] = SeriesIds[user].Select(
                                a =>
                                {
                                    var id = RepoFactory.AnimeSeries.GetByID(a)?.TopLevelAnimeGroup?.AnimeGroupID ?? -1;
                                    if (id == -1)
                                    {
                                        erroredSeries.Add(a);
                                    }

                                    return id;
                                }
                            ).Where(a => a != -1)
                            .ToHashSet();
                    }
                }
            );
            foreach (var id in erroredSeries.OrderBy(a => a).ToList())
            {
                var ser = RepoFactory.AnimeSeries.GetByID(id);
                LogManager.GetCurrentClassLogger()
                    .Error("While calculating group filters, an AnimeSeries without a group was found: " +
                           (ser?.GetSeriesName() ?? id.ToString()));
            }
        }
        else
        {
            EvaluateAnimeGroups();

            WriteLock(
                () =>
                {
                    foreach (var user in GroupsIds.Keys)
                    {
                        var ids = GroupsIds[user];
                        SeriesIds[user] = ids.SelectMany(
                                a =>
                                    RepoFactory.AnimeGroup.GetByID(a)?.GetAllSeries()
                                        ?.Select(b => b?.AnimeSeriesID ?? -1)
                            )
                            .Where(a => a != -1).ToHashSet();
                    }
                }
            );
        }

        if ((FilterType & (int)GroupFilterType.Tag) == (int)GroupFilterType.Tag)
        {
            GroupFilterName = GroupFilterName.Replace('`', '\'');
        }
    }

    private void EvaluateAnimeGroups()
    {
        var users = RepoFactory.JMMUser.GetAll();
        // make sure the user has not filtered this out
        var allGroupsIds = RepoFactory.AnimeGroup.GetAll().Select(a => a.AnimeGroupID).ToHashSet();
        foreach (var grp in RepoFactory.AnimeGroup.GetAllTopLevelGroups())
        {
            foreach (var user in users)
            {
                CalculateGroupFilterGroups(allGroupsIds, grp.GetUserContract(user.JMMUserID, cloned: false), user);
            }
        }
    }

    private void EvaluateAnimeSeries()
    {
        var users = RepoFactory.JMMUser.GetAll();
        var allSeries = RepoFactory.AnimeSeries.GetAll();
        var allSeriesIds = allSeries.Select(a => a.AnimeSeriesID).ToHashSet();
        foreach (var ser in allSeries)
        {
            if (ser.Contract == null) ser.UpdateContract();

            if (ser.Contract == null) continue;

            CalculateGroupFilterSeries(allSeriesIds, ser.Contract, null); //Default no filter for JMM Client
            foreach (var user in users)
            {
                CalculateGroupFilterSeries(allSeriesIds, ser.GetUserContract(user.JMMUserID, cloned: false), user);
            }
        }
    }

    public static CL_GroupFilter EvaluateContract(CL_GroupFilter gfc)
    {
        var gf = FromClient(gfc);
        if (gf.ApplyToSeries == 1)
        {
            gf.EvaluateAnimeSeries();

            gf.WriteLock(
                () =>
                {
                    foreach (var user in gf.SeriesIds.Keys)
                    {
                        gf.GroupsIds[user] = gf.SeriesIds[user]
                            .Select(a => RepoFactory.AnimeSeries.GetByID(a)?.TopLevelAnimeGroup?.AnimeGroupID ?? -1)
                            .Where(a => a != -1).ToHashSet();
                    }
                }
            );
        }
        else
        {
            gf.EvaluateAnimeGroups();

            gf.WriteLock(
                () =>
                {
                    foreach (var user in gf.GroupsIds.Keys)
                    {
                        gf.SeriesIds[user] = gf.GroupsIds[user]
                            .SelectMany(a =>
                                RepoFactory.AnimeGroup.GetByID(a)?.GetAllSeries()?.Select(b => b?.AnimeSeriesID ?? -1))
                            .Where(a => a != -1).ToHashSet();
                    }
                }
            );
        }

        return gf.ToClient();
    }


    public bool EvaluateGroupFilter(CL_AnimeGroup_User contractGroup, JMMUser curUser)
    {
        //Directories don't count
        if ((FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)
        {
            return false;
        }

        if (contractGroup?.Stat_AllTags == null)
        {
            return false;
        }

        if (curUser?.GetHideCategories().FindInEnumerable(contractGroup.Stat_AllTags) ?? false)
        {
            return false;
        }

        // sub groups don't count
        if (contractGroup.AnimeGroupParentID.HasValue)
        {
            return false;
        }

        // first check for anime groups which are included exluded every time
        foreach (var gfc in Conditions)
        {
            if (gfc.GetConditionTypeEnum() != GroupFilterConditionType.AnimeGroup)
            {
                continue;
            }

            int.TryParse(gfc.ConditionParameter, out var groupID);
            if (groupID == 0)
            {
                break;
            }


            if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Equals &&
                groupID == contractGroup.AnimeGroupID)
            {
                return true;
            }

            if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotEquals &&
                groupID != contractGroup.AnimeGroupID)
            {
                return true;
            }

            return false;
        }

        var exclude = BaseCondition == (int)GroupFilterBaseCondition.Exclude;

        return Conditions.All(gfc => exclude ^ EvaluateConditions(contractGroup, gfc));
    }

    private bool EvaluateConditions(CL_AnimeGroup_User contractGroup, GroupFilterCondition gfc)
    {
        var style = NumberStyles.Number;
        var culture = CultureInfo.InvariantCulture;
        switch (gfc.GetConditionTypeEnum())
        {
            case GroupFilterConditionType.Favourite:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && contractGroup.IsFave == 0)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && contractGroup.IsFave == 1)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.MissingEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    (contractGroup.MissingEpisodeCount > 0 || contractGroup.MissingEpisodeCountGroups > 0) ==
                    false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    (contractGroup.MissingEpisodeCount > 0 || contractGroup.MissingEpisodeCountGroups > 0))
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.MissingEpisodesCollecting:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.MissingEpisodeCountGroups > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.MissingEpisodeCountGroups > 0)
                {
                    return false;
                }

                break;
            case GroupFilterConditionType.Tag:
                var tags =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList();
                var tagsFound =
                    tags.Any(
                        a => contractGroup.Stat_AllTags.Contains(a));
                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.In ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include) && !tagsFound)
                {
                    return false;
                }

                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude) && tagsFound)
                {
                    return false;
                }

                break;
            case GroupFilterConditionType.Year:
                var years = new HashSet<int>();
                var parameterStrings = gfc.ConditionParameter.Trim()
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var yearString in parameterStrings)
                {
                    if (int.TryParse(yearString.Trim(), out var year))
                    {
                        years.Add(year);
                    }
                }

                if (years.Count <= 0)
                {
                    return false;
                }

                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.In) &&
                    !contractGroup.Stat_AllYears.FindInEnumerable(years))
                {
                    return false;
                }

                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn) &&
                    contractGroup.Stat_AllYears.FindInEnumerable(years))
                {
                    return false;
                }

                break;
            case GroupFilterConditionType.Season:
                var paramStrings = gfc.ConditionParameter.Trim().Split(',');

                switch (gfc.GetConditionOperatorEnum())
                {
                    case GroupFilterOperator.Include:
                    case GroupFilterOperator.In:
                        return paramStrings.FindInEnumerable(contractGroup.Stat_AllSeasons);
                    case GroupFilterOperator.Exclude:
                    case GroupFilterOperator.NotIn:
                        return !paramStrings.FindInEnumerable(contractGroup.Stat_AllSeasons);
                }

                break;
            case GroupFilterConditionType.HasWatchedEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.WatchedEpisodeCount > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.WatchedEpisodeCount > 0)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.HasUnwatchedEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.UnwatchedEpisodeCount > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.UnwatchedEpisodeCount > 0)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedTvDBInfo:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_HasTvDBLink == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasTvDBLink)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedTraktInfo:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_HasTraktLink == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasTraktLink)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedMALInfo:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_HasMALLink == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasMALLink)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedMovieDBInfo:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_HasMovieDBLink == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasMovieDBLink)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    !contractGroup.Stat_HasMovieDBOrTvDBLink)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasMovieDBOrTvDBLink)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.CompletedSeries:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_IsComplete == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_IsComplete)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.FinishedAiring:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_HasFinishedAiring == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_HasFinishedAiring)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserVoted:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_UserVotePermanent.HasValue == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_UserVotePermanent.HasValue)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserVotedAny:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractGroup.Stat_UserVoteOverall.HasValue == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractGroup.Stat_UserVoteOverall.HasValue)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AirDate:
                DateTime filterDate;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDate = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDate = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.Stat_AirDate_Max.Value < filterDate)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.Stat_AirDate_Min.Value > filterDate)
                    {
                        return false;
                    }
                }

                break;
            case GroupFilterConditionType.LatestEpisodeAirDate:
                DateTime filterDateEpisodeLastAired;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpisodeLastAired = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpisodeLastAired = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractGroup.LatestEpisodeAirDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.LatestEpisodeAirDate.Value < filterDateEpisodeLastAired)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractGroup.LatestEpisodeAirDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.LatestEpisodeAirDate.Value > filterDateEpisodeLastAired)
                    {
                        return false;
                    }
                }

                break;
            case GroupFilterConditionType.SeriesCreatedDate:
                DateTime filterDateSeries;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateSeries = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateSeries = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractGroup.Stat_SeriesCreatedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.Stat_SeriesCreatedDate.Value < filterDateSeries)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractGroup.Stat_SeriesCreatedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.Stat_SeriesCreatedDate.Value > filterDateSeries)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeWatchedDate:
                DateTime filterDateEpsiodeWatched;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpsiodeWatched = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpsiodeWatched = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractGroup.WatchedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.WatchedDate.Value < filterDateEpsiodeWatched)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (contractGroup?.WatchedDate == null)
                    {
                        return false;
                    }

                    if (contractGroup.WatchedDate.Value > filterDateEpsiodeWatched)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeAddedDate:
                DateTime filterDateEpisodeAdded;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpisodeAdded = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpisodeAdded = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractGroup.EpisodeAddedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.EpisodeAddedDate.Value < filterDateEpisodeAdded)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractGroup.EpisodeAddedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractGroup.EpisodeAddedDate.Value > filterDateEpisodeAdded)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeCount:
                int.TryParse(gfc.ConditionParameter, out var epCount);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan &&
                    contractGroup.Stat_EpisodeCount < epCount)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan &&
                    contractGroup.Stat_EpisodeCount > epCount)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AniDBRating:
                decimal.TryParse(gfc.ConditionParameter, style, culture, out var dRating);
                var thisRating = contractGroup.Stat_AniDBRating / 100;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan && thisRating < dRating)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan && thisRating > dRating)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserRating:
                if (!contractGroup.Stat_UserVoteOverall.HasValue)
                {
                    return false;
                }

                decimal.TryParse(gfc.ConditionParameter, style, culture, out var dUserRating);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan &&
                    contractGroup.Stat_UserVoteOverall.Value < dUserRating)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan &&
                    contractGroup.Stat_UserVoteOverall.Value > dUserRating)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.CustomTags:
                var ctags =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundTag = ctags.FindInEnumerable(contractGroup.Stat_AllCustomTags);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundTag)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundTag)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AnimeType:
                var ctypes =
                    gfc.ConditionParameter
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => ((int)Commons.Extensions.Models.RawToType(a)).ToString())
                        .ToList();
                var foundAnimeType = ctypes.FindInEnumerable(contractGroup.Stat_AnimeTypes);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundAnimeType)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundAnimeType)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.VideoQuality:
                var vqs =
                    gfc.ConditionParameter
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundVid = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality);
                var foundVidAllEps = vqs.FindInEnumerable(contractGroup.Stat_AllVideoQuality_Episodes);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundVid)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundVid)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.InAllEpisodes && !foundVidAllEps)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotInAllEpisodes && foundVidAllEps)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AudioLanguage:
                var als =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundLang = als.FindInEnumerable(contractGroup.Stat_AudioLanguages);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundLang)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundLang)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.SubtitleLanguage:
                var ass =
                    gfc.ConditionParameter
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundSub = ass.FindInEnumerable(contractGroup.Stat_SubtitleLanguages);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundSub)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundSub)
                {
                    return false;
                }

                break;
        }

        return true;
    }

    public bool EvaluateGroupFilter(CL_AnimeSeries_User contractSerie, JMMUser curUser)
    {
        //Directories don't count
        if ((FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)
        {
            return false;
        }

        if (contractSerie?.AniDBAnime?.AniDBAnime == null)
        {
            return false;
        }

        if (curUser?.GetHideCategories().FindInEnumerable(contractSerie.AniDBAnime.AniDBAnime.GetAllTags()) ??
            false)
        {
            return false;
        }

        var exclude = BaseCondition == (int)GroupFilterBaseCondition.Exclude;

        return Conditions.All(gfc => exclude ^ EvaluateConditions(contractSerie, gfc));
    }

    private bool EvaluateConditions(CL_AnimeSeries_User contractSerie, GroupFilterCondition gfc)
    {
        var style = NumberStyles.Number;
        var culture = CultureInfo.InvariantCulture;

        switch (gfc.GetConditionTypeEnum())
        {
            case GroupFilterConditionType.MissingEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    (contractSerie.MissingEpisodeCount > 0 || contractSerie.MissingEpisodeCountGroups > 0) ==
                    false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    (contractSerie.MissingEpisodeCount > 0 || contractSerie.MissingEpisodeCountGroups > 0))
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.MissingEpisodesCollecting:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractSerie.MissingEpisodeCountGroups > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractSerie.MissingEpisodeCountGroups > 0)
                {
                    return false;
                }

                break;
            case GroupFilterConditionType.Tag:
                var tags =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList();
                var tagsFound =
                    tags.Any(a => contractSerie.AniDBAnime.AniDBAnime.GetAllTags().Contains(a));
                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.In ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include) && !tagsFound)
                {
                    return false;
                }

                if ((gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn ||
                     gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude) && tagsFound)
                {
                    return false;
                }

                break;
            case GroupFilterConditionType.Year:
                var years = new HashSet<int>();
                var parameterStrings = gfc.ConditionParameter.Trim().Split(',');
                foreach (var yearString in parameterStrings)
                {
                    if (int.TryParse(yearString.Trim(), out var paramYear))
                    {
                        years.Add(paramYear);
                    }
                }

                if (years.Count <= 0)
                {
                    return false;
                }

                switch (gfc.GetConditionOperatorEnum())
                {
                    case GroupFilterOperator.Include:
                    case GroupFilterOperator.In:
                        if (years.Any(year => contractSerie.AniDBAnime.IsInYear(year)))
                        {
                            return true;
                        }

                        return false;
                    case GroupFilterOperator.Exclude:
                    case GroupFilterOperator.NotIn:
                        if (years.Any(year => contractSerie.AniDBAnime.IsInYear(year)))
                        {
                            return false;
                        }

                        return true;
                }

                break;
            case GroupFilterConditionType.Season:
                var paramStrings = gfc.ConditionParameter.Trim().Split(',').Select(a =>
                {
                    var b = a.Trim().Split(' ');
                    if (!Enum.TryParse(b[0], out AnimeSeason season))
                    {
                        return null;
                    }

                    if (!int.TryParse(b[1], out var year))
                    {
                        return null;
                    }

                    return Tuple.Create(season, year);
                }).Where(a => a != null).ToArray();

                switch (gfc.GetConditionOperatorEnum())
                {
                    case GroupFilterOperator.Include:
                    case GroupFilterOperator.In:
                        return paramStrings.Any(a => contractSerie?.AniDBAnime?.IsInSeason(a.Item1, a.Item2) ?? false);
                    case GroupFilterOperator.Exclude:
                    case GroupFilterOperator.NotIn:
                        return !paramStrings.Any(a => contractSerie?.AniDBAnime?.IsInSeason(a.Item1, a.Item2) ?? false);
                }

                break;
            case GroupFilterConditionType.HasWatchedEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractSerie.WatchedEpisodeCount > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractSerie.WatchedEpisodeCount > 0)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.HasUnwatchedEpisodes:
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include &&
                    contractSerie.UnwatchedEpisodeCount > 0 == false)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude &&
                    contractSerie.UnwatchedEpisodeCount > 0)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedTvDBInfo:
                if (contractSerie.AniDBAnime.AniDBAnime.AnimeType == (int)AnimeType.Movie ||
                    contractSerie.AniDBAnime.AniDBAnime.Restricted > 0)
                {
                    return false;
                }

                var tvDBInfoMissing = !RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(contractSerie.AniDB_ID).Any();
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && tvDBInfoMissing)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && !tvDBInfoMissing)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedMALInfo:
                var malMissing = contractSerie.CrossRefAniDBMAL == null ||
                                 contractSerie.CrossRefAniDBMAL.Count == 0;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && malMissing)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && !malMissing)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedMovieDBInfo:
                if (contractSerie.AniDBAnime.AniDBAnime.AnimeType != (int)AnimeType.Movie ||
                    contractSerie.AniDBAnime.AniDBAnime.Restricted > 0)
                {
                    return false;
                }

                var movieMissing = contractSerie.CrossRefAniDBMovieDB == null;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && movieMissing)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && !movieMissing)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                // return true if excluding
                if (contractSerie.AniDBAnime.AniDBAnime.Restricted > 0)
                {
                    return false;
                }

                var movieLinkMissing = contractSerie.CrossRefAniDBMovieDB == null;
                var tvlinkMissing = !RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(contractSerie.AniDB_ID).Any();
                var bothMissing = movieLinkMissing && tvlinkMissing;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && bothMissing)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && !bothMissing)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.CompletedSeries:
                var completed = contractSerie.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                                contractSerie.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
                                !(contractSerie.MissingEpisodeCount > 0 ||
                                  contractSerie.MissingEpisodeCountGroups > 0);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && !completed)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && completed)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.FinishedAiring:
                var finished = contractSerie.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                               contractSerie.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && !finished)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && finished)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserVoted:
                var voted = contractSerie.AniDBAnime.UserVote != null &&
                            contractSerie.AniDBAnime.UserVote.VoteType == (int)AniDBVoteType.Anime;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && !voted)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && voted)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserVotedAny:
                var votedany = contractSerie.AniDBAnime.UserVote != null;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Include && !votedany)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.Exclude && votedany)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AirDate:
                DateTime filterDate;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDate = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDate = GroupFilterHelper.GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractSerie.AniDBAnime.AniDBAnime.AirDate.HasValue ||
                        contractSerie.AniDBAnime.AniDBAnime.AirDate.Value < filterDate)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractSerie.AniDBAnime.AniDBAnime.AirDate.HasValue ||
                        contractSerie.AniDBAnime.AniDBAnime.AirDate.Value > filterDate)
                    {
                        return false;
                    }
                }

                break;
            case GroupFilterConditionType.LatestEpisodeAirDate:
                DateTime filterDateEpisodeLastAired;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpisodeLastAired = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpisodeLastAired = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractSerie.LatestEpisodeAirDate.HasValue)
                    {
                        return false;
                    }

                    if (contractSerie.LatestEpisodeAirDate.Value < filterDateEpisodeLastAired)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractSerie.LatestEpisodeAirDate.HasValue)
                    {
                        return false;
                    }

                    if (contractSerie.LatestEpisodeAirDate.Value > filterDateEpisodeLastAired)
                    {
                        return false;
                    }
                }

                break;
            case GroupFilterConditionType.SeriesCreatedDate:
                DateTime filterDateSeries;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateSeries = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateSeries = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (contractSerie.DateTimeCreated < filterDateSeries)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (contractSerie.DateTimeCreated > filterDateSeries)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeWatchedDate:
                DateTime filterDateEpsiodeWatched;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpsiodeWatched = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpsiodeWatched = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractSerie.WatchedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractSerie.WatchedDate.Value < filterDateEpsiodeWatched)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (contractSerie?.WatchedDate == null)
                    {
                        return false;
                    }

                    if (contractSerie.WatchedDate.Value > filterDateEpsiodeWatched)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeAddedDate:
                DateTime filterDateEpisodeAdded;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    int.TryParse(gfc.ConditionParameter, out var days);
                    filterDateEpisodeAdded = DateTime.Today.AddDays(0 - days);
                }
                else
                {
                    filterDateEpisodeAdded = GetDateFromString(gfc.ConditionParameter);
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan ||
                    gfc.GetConditionOperatorEnum() == GroupFilterOperator.LastXDays)
                {
                    if (!contractSerie.EpisodeAddedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractSerie.EpisodeAddedDate.Value < filterDateEpisodeAdded)
                    {
                        return false;
                    }
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan)
                {
                    if (!contractSerie.EpisodeAddedDate.HasValue)
                    {
                        return false;
                    }

                    if (contractSerie.EpisodeAddedDate.Value > filterDateEpisodeAdded)
                    {
                        return false;
                    }
                }

                break;

            case GroupFilterConditionType.EpisodeCount:
                int.TryParse(gfc.ConditionParameter, out var epCount);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan &&
                    contractSerie.AniDBAnime.AniDBAnime.EpisodeCount < epCount)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan &&
                    contractSerie.AniDBAnime.AniDBAnime.EpisodeCount > epCount)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AniDBRating:
                decimal.TryParse(gfc.ConditionParameter, style, culture, out var dRating);
                var totalVotes = contractSerie.AniDBAnime.AniDBAnime.VoteCount +
                                 contractSerie.AniDBAnime.AniDBAnime.TempVoteCount;
                decimal totalRating = contractSerie.AniDBAnime.AniDBAnime.Rating *
                                      contractSerie.AniDBAnime.AniDBAnime.VoteCount +
                                      contractSerie.AniDBAnime.AniDBAnime.TempRating *
                                      contractSerie.AniDBAnime.AniDBAnime.TempVoteCount;
                var thisRating = totalVotes == 0 ? 0 : totalRating / totalVotes / 100;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan && thisRating < dRating)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan && thisRating > dRating)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.UserRating:
                decimal.TryParse(gfc.ConditionParameter, style, culture, out var dUserRating);
                decimal val = contractSerie.AniDBAnime.UserVote?.VoteValue ?? 0;
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.GreaterThan && val < dUserRating)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.LessThan && val > dUserRating)
                {
                    return false;
                }

                break;


            case GroupFilterConditionType.CustomTags:
                var ctags =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundTag =
                    ctags.FindInEnumerable(contractSerie.AniDBAnime.CustomTags.Select(a => a.TagName));
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundTag)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundTag)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AnimeType:
                var ctypes =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(
                            a => ((int)Commons.Extensions.Models.RawToType(a.ToLowerInvariant())).ToString())
                        .ToList();
                var foundAnimeType = ctypes.Contains(contractSerie.AniDBAnime.AniDBAnime.AnimeType.ToString());
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundAnimeType)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundAnimeType)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.VideoQuality:
                var vqs =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundVid = vqs.FindInEnumerable(contractSerie.AniDBAnime.Stat_AllVideoQuality);
                var foundVidAllEps =
                    vqs.FindInEnumerable(contractSerie.AniDBAnime.Stat_AllVideoQuality_Episodes);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundVid)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundVid)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.InAllEpisodes && !foundVidAllEps)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotInAllEpisodes && foundVidAllEps)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.AudioLanguage:
                var als =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundLang = als.FindInEnumerable(contractSerie.AniDBAnime.Stat_AudioLanguages);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundLang)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundLang)
                {
                    return false;
                }

                break;

            case GroupFilterConditionType.SubtitleLanguage:
                var ass =
                    gfc.ConditionParameter.Trim()
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.ToLowerInvariant().Trim())
                        .ToList();
                var foundSub = ass.FindInEnumerable(contractSerie.AniDBAnime.Stat_AudioLanguages);
                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.In && !foundSub)
                {
                    return false;
                }

                if (gfc.GetConditionOperatorEnum() == GroupFilterOperator.NotIn && foundSub)
                {
                    return false;
                }

                break;
        }

        return true;
    }

    public static DateTime GetDateFromString(string sDate)
    {
        try
        {
            var year = int.Parse(sDate.Substring(0, 4));
            var month = int.Parse(sDate.Substring(4, 2));
            var day = int.Parse(sDate.Substring(6, 2));

            return new DateTime(year, month, day);
        }
        catch
        {
            return DateTime.Today;
        }
    }

    /// <summary>
    /// Updates the <see cref="GroupsIdsString"/> and/or <see cref="SeriesIdsString"/> properties
    /// based on the current contents of <see cref="GroupsIds"/> and <see cref="SeriesIds"/>.
    /// </summary>
    /// <param name="updateGroups"><c>true</c> to update <see cref="GroupsIdsString"/>; otherwise, <c>false</c>.</param>
    /// <param name="updateSeries"><c>true</c> to update <see cref="SeriesIds"/>; otherwise, <c>false</c>.</param>
    public void UpdateEntityReferenceStrings(bool updateGroups = true, bool updateSeries = true)
    {
        WriteLock(
            () =>
            {
                if (updateGroups)
                {
                    GroupsIdsString = JsonConvert.SerializeObject(GroupsIds);
                    GroupsIdsVersion = GROUPFILTER_VERSION;
                }

                if (updateSeries)
                {
                    SeriesIdsString = JsonConvert.SerializeObject(SeriesIds);
                    SeriesIdsVersion = SERIEFILTER_VERSION;
                }
            }
        );
    }

    public void QueueUpdate()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var cmdRefreshGroupFilter =
            commandFactory.Create<CommandRequest_RefreshGroupFilter>(c => c.GroupFilterID = GroupFilterID);
        cmdRefreshGroupFilter.Save();
    }

    public override bool Equals(object obj)
    {
        var other = obj as SVR_GroupFilter;
        if (other?.ApplyToSeries != ApplyToSeries)
        {
            return false;
        }

        if (other.BaseCondition != BaseCondition)
        {
            return false;
        }

        if (other.FilterType != FilterType)
        {
            return false;
        }

        if (other.InvisibleInClients != InvisibleInClients)
        {
            return false;
        }

        if (other.Locked != Locked)
        {
            return false;
        }

        if (other.ParentGroupFilterID != ParentGroupFilterID)
        {
            return false;
        }

        if (other.GroupFilterName != GroupFilterName)
        {
            return false;
        }

        if (other.SortingCriteria != SortingCriteria)
        {
            return false;
        }

        if (Conditions == null || Conditions.Count == 0)
        {
            Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(GroupFilterID);
            RepoFactory.GroupFilter.Save(this);
        }

        if (other.Conditions == null || other.Conditions.Count == 0)
        {
            other.Conditions = RepoFactory.GroupFilterCondition.GetByGroupFilterID(other.GroupFilterID);
            RepoFactory.GroupFilter.Save(other);
        }

        if (Conditions != null && other.Conditions != null)
        {
            if (!Conditions.ContentEquals(other.Conditions))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return 0; // Always use equals
    }

    private void ReadLock(Action action)
    {
        _lock.EnterReadLock();
        try
        {
            action.Invoke();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private T ReadLock<T>(Func<T> action)
    {
        _lock.EnterReadLock();
        try
        {
            return action.Invoke();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void WriteLock(Action action)
    {
        _lock.EnterWriteLock();
        try
        {
            action.Invoke();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private T WriteLock<T>(Func<T> action)
    {
        _lock.EnterWriteLock();
        try
        {
            return action.Invoke();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
