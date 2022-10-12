using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// A Group Filter. This is how Shoko serves and organizes Series/Groups. They can be used to keep track of what
/// you're watching and many other things
/// </summary>
public class Filter : BaseModel
{
    /// <summary>
    /// The Filter ID. self explanatory
    /// </summary>
    public FilterIDs IDs { get; set; }

    /// <summary>
    /// Locked Filters cannot be edited
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Things like Seasons, Years, Tags, etc only count series individually, rather than by group
    /// </summary>
    public bool ApplyAtSeriesLevel { get; set; }

    /// <summary>
    /// Directory Filters have subfilters
    /// </summary>
    public bool Directory { get; set; }

    /// <summary>
    /// This determines whether to hide the filter in API queries. Things with this need to be explicitly asked for
    /// </summary>
    public bool Hidden { get; set; }

    public Filter() { }

    public Filter(HttpContext ctx, SVR_GroupFilter gf)
    {
        IDs = new FilterIDs { ID = gf.GroupFilterID, ParentFilter = gf.ParentGroupFilterID };
        Name = gf.GroupFilterName;
        var user = ctx.GetUser();
        Directory = ((GroupFilterType)gf.FilterType).HasFlag(GroupFilterType.Directory);
        // This can be used to exclude from client visibility for user
        Size = gf.GroupsIds.ContainsKey(user.JMMUserID) ? gf.GroupsIds[user.JMMUserID].Count : 0;
        if (Directory)
        {
            Size += RepoFactory.GroupFilter.GetByParentID(gf.GroupFilterID).Count;
        }

        ApplyAtSeriesLevel = gf.ApplyToSeries == 1;

        // It's never null, just marked Nullable for some reason
        Locked = gf.Locked != null && gf.Locked.Value == 1;

        Hidden = gf.InvisibleInClients == 1;
    }

    /// <summary>
    /// Get the Conditions for the Group Filter to be calculated
    /// </summary>
    /// <param name="gf"></param>
    /// <returns></returns>
    public static FilterConditions GetConditions(SVR_GroupFilter gf)
    {
        return new FilterConditions(gf);
    }

    /// <summary>
    /// Get the Sorting Criteria for the Group Filter. ORDER DOES MATTER
    /// </summary>
    /// <param name="gf"></param>
    /// <returns></returns>
    public static List<SortingCriteria> GetSortingCriteria(SVR_GroupFilter gf)
    {
        return gf.SortCriteriaList.Select(a => new SortingCriteria(a)).ToList();
    }

    public class FilterIDs : IDs
    {
        /// <summary>
        /// The <see cref="IDs.ID"/> of the parent <see cref="Filter"/>, if it has one.
        /// </summary>
        public int? ParentFilter { get; set; }
    }

    public class FilterConditions
    {
        /// <summary>
        /// List of Conditions. The order does not matter.
        /// </summary>
        public List<Condition> Conditions { get; set; }

        /// <summary>
        /// The BaseCondition, as referenced elsewhere. If this is true, then everything is calculated on an Exclusion basis.
        /// It essentially inverts the logic of all inside conditions.
        /// </summary>
        public bool InvertLogic { get; set; }

        public FilterConditions(SVR_GroupFilter gf)
        {
            Conditions = gf.Conditions.Select(a => new Condition(a)).ToList();
            InvertLogic = gf.BaseCondition == (int)GroupFilterBaseCondition.Exclude;
        }
    }

    public class Condition
    {
        /// <summary>
        /// Condition ID, not important for anything except changing or deleting
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Condition Type. What it does
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterConditionType Type { get; set; }

        /// <summary>
        /// Condition Operator, how it applies
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterOperator Operator { get; set; }

        /// <summary>
        /// The actual value to compare
        /// </summary>
        public string Parameter { get; set; }

        public Condition(GroupFilterCondition condition)
        {
            ID = condition.GroupFilterConditionID;
            Type = (GroupFilterConditionType)condition.ConditionType;
            Operator = (GroupFilterOperator)condition.ConditionOperator;
            Parameter = condition.ConditionParameter;
        }
    }

    /// <summary>
    /// Sorting Criteria hold info on how Group Filters sort their items. It is in a List to follow
    /// an OrderBy().ThenBy().ThenBy(), allowing consistent results with fallbacks.
    /// </summary>
    public class SortingCriteria
    {
        /// <summary>
        /// The sorting type. What it is sorted on
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterSorting Type { get; set; }

        /// <summary>
        /// Assumed Ascending unless this is specified. You must set this if you want highest rating, for example
        /// </summary>
        public bool Descending { get; set; }

        public SortingCriteria(GroupFilterSortingCriteria criteria)
        {
            Type = criteria.SortType;
            Descending = criteria.SortDirection == GroupFilterSortDirection.Desc;
        }
    }

    /// <summary>
    /// This is to be used sparingly. It is namely for saving new Filters and previewing the changes
    /// </summary>
    public class FullFilter : Filter, IFullModel<SVR_GroupFilter>
    {
        /// <summary>
        /// The Parent Filter ID. You don't need to know this otherwise, but if you are saving a new filter, it is important.
        /// </summary>
        public int ParentID { get; set; }

        /// <summary>
        /// The Filter conditions
        /// </summary>
        public FilterConditions Conditions { get; set; }

        /// <summary>
        /// The sorting criteria
        /// </summary>
        public List<SortingCriteria> Sorting { get; set; }

        /// <summary>
        /// Creates a server model compatible with the database. This does not calculate any cached data, such as groups and series.
        /// </summary>
        /// <returns></returns>
        public SVR_GroupFilter ToServerModel(SVR_GroupFilter existing = null)
        {
            var groupFilter = new SVR_GroupFilter
            {
                FilterType =
                    Directory
                        ? (int)(GroupFilterType.UserDefined | GroupFilterType.Directory)
                        : (int)GroupFilterType.UserDefined,
                ApplyToSeries = ApplyAtSeriesLevel ? 1 : 0,
                GroupFilterName = Name,
                InvisibleInClients = Hidden ? 1 : 0,
                ParentGroupFilterID = ParentID == 0 ? (int?)null : ParentID,
                // Conditions
                BaseCondition = (int)(Conditions.InvertLogic
                    ? GroupFilterBaseCondition.Exclude
                    : GroupFilterBaseCondition.Include),
                Conditions = Conditions.Conditions.Select(c =>
                {
                    var condition = new GroupFilterCondition();

                    return condition;
                }).ToList(),
                // Sorting
                SortCriteriaList = Sorting.Select(s =>
                {
                    var criteria = new GroupFilterSortingCriteria
                    {
                        SortType = s.Type,
                        SortDirection = s.Descending ? GroupFilterSortDirection.Desc : GroupFilterSortDirection.Asc
                    };
                    return criteria;
                }).ToList()
            };

            // Return new group filter.
            if (existing == null)
            {
                return groupFilter;
            }

            // Merge into existing group filter.
            existing.ApplyToSeries = groupFilter.ApplyToSeries;
            existing.ParentGroupFilterID = groupFilter.ParentGroupFilterID;
            existing.FilterType = groupFilter.FilterType;
            existing.InvisibleInClients = groupFilter.InvisibleInClients;
            if (groupFilter.GroupFilterName != null)
            {
                existing.GroupFilterName = groupFilter.GroupFilterName;
            }

            if (groupFilter.Conditions != null)
            {
                existing.BaseCondition = groupFilter.BaseCondition;
                existing.Conditions = groupFilter.Conditions;
            }

            if (groupFilter.SortCriteriaList != null)
            {
                existing.SortCriteriaList = groupFilter.SortCriteriaList;
            }

            return existing;
        }
    }
}
