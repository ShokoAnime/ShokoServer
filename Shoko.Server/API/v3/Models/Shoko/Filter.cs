using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shoko.Server.API.v3
{
    public class Filter : BaseModel
    {
        /// <summary>
        /// The Filter ID. self explanatory
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// Locked Filters cannot be edited
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// Things like Seasons, Years, Tags, etc only count series individually, rather than by group
        /// </summary>
        public bool ApplyAtSeriesLevel { get; set; }
        
        /// <summary>
        /// The Group Filter Type. This is a flag to determine certain things, like if it's a directory filter
        /// </summary>
        public GroupFilterType Type { get; set; }
        
        /// <summary>
        /// This determines whether to hide the filter in API queries. Things with this need to be explicitly asked for
        /// </summary>
        public bool HideInAPI { get; set; }

        public Filter(HttpContext ctx, SVR_GroupFilter gf)
        {
            ID = gf.GroupFilterID;
            Name = gf.GroupFilterName;
            SVR_JMMUser user = ctx.GetUser();
            Type = (GroupFilterType) gf.FilterType;
            // This can be used to exclude from client visibility for user
            Size = gf.GroupsIds.ContainsKey(user.JMMUserID) ? gf.GroupsIds[user.JMMUserID].Count : 0;
            if (Size == 0 && Type.HasFlag(GroupFilterType.Directory))
                Size = RepoFactory.GroupFilter.GetByParentID(gf.GroupFilterID).Count;

            ApplyAtSeriesLevel = gf.ApplyToSeries == 1;
            
            // It's never null, just marked Nullable for some reason
            Locked = gf.Locked != null && gf.Locked.Value == 1;

            HideInAPI = gf.InvisibleInClients == 1;
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
                InvertLogic = gf.BaseCondition == (int) GroupFilterBaseCondition.Exclude;
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
            public GroupFilterConditionType Type { get; set; }

            /// <summary>
            /// Condition Operator, how it applies
            /// </summary>
            public GroupFilterOperator Operator { get; set; }
            
            /// <summary>
            /// The actual value to compare
            /// </summary>
            public string Parameter { get; set; }
            public Condition(GroupFilterCondition condition)
            {
                ID = condition.GroupFilterConditionID;
                Type = (GroupFilterConditionType) condition.ConditionType;
                Operator = (GroupFilterOperator) condition.ConditionOperator;
                Parameter = condition.ConditionParameter;
            }
        }

        public class SortingCriteria
        {
            public GroupFilterSorting Type { get; set; }
            public bool Descending { get; set; }
            public SortingCriteria(GroupFilterSortingCriteria criteria)
            {
                Type = criteria.SortType;
                Descending = criteria.SortDirection == GroupFilterSortDirection.Desc;
            }
        }
    }
}