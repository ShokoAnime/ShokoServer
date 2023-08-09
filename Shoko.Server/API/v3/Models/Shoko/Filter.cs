using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// A Filter. This is how Shoko serves and organizes Series/Groups. They can be
/// used to keep track of what you're watching and many other things.
/// </summary>
public class Filter : BaseModel
{
    /// <summary>
    /// The Filter ID.
    /// </summary>
    public FilterIDs IDs { get; set; }

    /// <summary>
    /// Indicates the filter cannot be edited by a user.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Indicates the filter should be a directory filter.
    /// </summary>
    /// <remarks>
    /// A directory filter cannot have any conditions and/or sorting
    /// attached to it. And changing an existing filter
    /// </remarks>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// Indicates that the filter is inverted and all conditions applied
    /// to it will be used to exclude groups and series instead of
    /// include them.
    /// </summary>
    public bool IsInverted { get; set; }

    /// <summary>
    /// Indicates the filter should be hidden unless explictly requested. This will hide the filter from the normal UIs.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Inidcates the filter should be applied at the series level.
    /// Filter conditions like like Seasons, Years, Tags, etc only count series individually, rather than by group.
    /// </summary>
    public bool ApplyAtSeriesLevel { get; set; }

    /// <summary>
    /// List of Conditions. Order doesn't matter.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<FilterCondition>? Conditions { get; set; }

    /// <summary>
    /// The sorting criteria. Order matters.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<SortingCriteria>? Sorting { get; set; }

    public Filter(HttpContext ctx, SVR_GroupFilter groupFilter, bool fullModel = false)
    {
        var user = ctx.GetUser();

        IDs = new FilterIDs { ID = groupFilter.GroupFilterID, ParentFilter = groupFilter.ParentGroupFilterID };
        Name = groupFilter.GroupFilterName;
        IsLocked = groupFilter.IsLocked;
        IsDirectory = groupFilter.IsDirectory;
        IsInverted = groupFilter.BaseCondition == (int)GroupFilterBaseCondition.Exclude;
        IsHidden = groupFilter.IsHidden;
        ApplyAtSeriesLevel = groupFilter.ApplyToSeries == 1;
        if (fullModel)
        {
            Conditions = groupFilter.Conditions.Select(condition => new FilterCondition(condition)).ToList();
            Sorting = groupFilter.SortCriteriaList.Select(sort => new SortingCriteria(sort)).ToList();
        }
        Size = IsDirectory ? (
            RepoFactory.GroupFilter.GetByParentID(groupFilter.GroupFilterID).Count
        ) : (
            groupFilter.GroupsIds.TryGetValue(user.JMMUserID, out var groupSet) ? groupSet.Count : 0
        );
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

    public class FilterCondition
    {
        /// <summary>
        /// Condition Type. What it does
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterConditionType Type { get; set; }

        /// <summary>
        /// Condition Operator, how it applies
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterOperator Operator { get; set; }

        /// <summary>
        /// The actual value to compare
        /// </summary>
        public string Parameter { get; set; } = string.Empty;

        public FilterCondition() { }

        public FilterCondition(GroupFilterCondition condition)
        {
            Type = (GroupFilterConditionType)condition.ConditionType;
            Operator = (GroupFilterOperator)condition.ConditionOperator;
            Parameter = condition.ConditionParameter ?? string.Empty;
        }
    }

    /// <summary>
    /// Sorting Criteria hold info on how Group Filters sort their items.
    /// It is in a List to follow an OrderBy().ThenBy().ThenBy(), allowing
    /// consistent results with fallbacks.
    /// </summary>
    public class SortingCriteria
    {
        /// <summary>
        /// The sorting type. What it is sorted on
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public GroupFilterSorting Type { get; set; }

        /// <summary>
        /// Assumed Ascending unless this is specified. You must set this if you want highest rating, for example
        /// </summary>
        [Required]
        public bool IsInverted { get; set; }

        public SortingCriteria() { }

        public SortingCriteria(GroupFilterSortingCriteria criteria)
        {
            Type = criteria.SortType;
            IsInverted = criteria.SortDirection == GroupFilterSortDirection.Desc;
        }
    }

    public class Input
    {
        /// <summary>
        /// Used for creating new filters, updating existing filters, and/or
        /// updating the live filter.
        /// </summary>
        public class CreateOrUpdateFilterBody
        {
            /// <summary>
            /// The filter name.
            /// </summary>
            /// <value></value>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// The id of the parent filter. If you want to add/move this filter
            /// as a sub-filter to an existing directory filter.
            /// </summary>
            public int? ParentID { get; set; }

            /// <summary>
            /// Indicates the filter should be a directory filter.
            /// </summary>
            /// <remarks>
            /// A directory filter cannot have any conditions and/or sorting
            /// attached to it. And changing an existing filter
            /// </remarks>
            public bool IsDirectory { get; set; }

            /// <summary>
            /// Indicates that the filter is inverted and all conditions applied
            /// to it will be used to exclude groups and series instead of
            /// include them.
            /// </summary>
            public bool IsInverted { get; set; }

            /// <summary>
            /// Indicates the filter should be hidden unless explictly requested. This will hide the filter from the normal UIs.
            /// </summary>
            public bool IsHidden { get; set; }

            /// <summary>
            /// Inidcates the filter should be applied at the series level.
            /// Filter conditions like like Seasons, Years, Tags, etc only count series individually, rather than by group.
            /// </summary>
            public bool ApplyAtSeriesLevel { get; set; }

            /// <summary>
            /// List of Conditions. Order doesn't matter.
            /// </summary>
            public List<FilterCondition>? Conditions { get; set; }

            /// <summary>
            /// The sorting criteria. Order matters.
            /// </summary>
            public List<SortingCriteria>? Sorting { get; set; }

            public CreateOrUpdateFilterBody() { }

            public CreateOrUpdateFilterBody(SVR_GroupFilter groupFilter)
            {
                Name = groupFilter.GroupFilterName;
                ParentID = groupFilter.ParentGroupFilterID;
                IsDirectory = groupFilter.IsDirectory;
                IsInverted = groupFilter.BaseCondition == (int)GroupFilterBaseCondition.Exclude;
                IsHidden = groupFilter.IsHidden;
                ApplyAtSeriesLevel = groupFilter.ApplyToSeries == 1;
                if (!IsDirectory)
                {
                    Conditions = groupFilter.Conditions.Select(condition => new FilterCondition(condition)).ToList();
                    Sorting = groupFilter.SortCriteriaList.Select(sort => new SortingCriteria(sort)).ToList();
                }
            }

            public Filter? MergeWithExisting(HttpContext ctx, SVR_GroupFilter groupFilter, ModelStateDictionary modelState, bool skipSave = false)
            {
                if (groupFilter.IsLocked)
                    modelState.AddModelError("IsLocked", "Filter is locked.");

                // Defer to `null` if the id is `0`.
                if (ParentID.HasValue && ParentID.Value == 0)
                    ParentID = null;

                if (ParentID.HasValue)
                {
                    var parentFilter = RepoFactory.GroupFilter.GetByID(ParentID.Value);
                    if (parentFilter == null)
                    {
                        modelState.AddModelError(nameof(ParentID), $"Unable to find parent filter with id {ParentID.Value}");
                    }
                    else
                    {
                        if (parentFilter.IsLocked)
                            modelState.AddModelError(nameof(ParentID), $"Unable to add a sub-filter to a filter that is locked.");

                        if (!parentFilter.IsDirectory)
                            modelState.AddModelError(nameof(ParentID), $"Unable to add a sub-filter to a filter that is not a directorty filter.");
                    }
                }

                if (IsDirectory)
                {
                    if (IsInverted)
                        modelState.AddModelError(nameof(IsInverted), "Cannot invert the filter conditions for a directory filter.");

                    if (Conditions != null && Conditions.Count > 0)
                        modelState.AddModelError(nameof(Conditions), "Directory filters cannot have any conditions applied to them.");

                    if (Sorting != null && Sorting.Count > 0)
                        modelState.AddModelError(nameof(Sorting), "Directory filters cannot have custom sorting applied to them.");
                }
                else
                {
                    var subFilters = groupFilter.GroupFilterID != 0 ? RepoFactory.GroupFilter.GetByParentID(groupFilter.GroupFilterID) : new();
                    if (subFilters.Count > 0)
                        modelState.AddModelError(nameof(IsDirectory), "Cannot turn a directory filter with sub-filters into a normal filter without first removing the sub-filters");
                }

                // Return now if we encountered any validation errors.
                if (!modelState.IsValid)
                    return null;

                groupFilter.ParentGroupFilterID = ParentID;
                groupFilter.FilterType = (int)(IsDirectory ? GroupFilterType.UserDefined | GroupFilterType.Directory : GroupFilterType.UserDefined);
                groupFilter.GroupFilterName = Name ?? string.Empty;
                groupFilter.IsHidden = IsHidden;
                groupFilter.ApplyToSeries = ApplyAtSeriesLevel ? 1 : 0;
                if (IsDirectory)
                {
                    groupFilter.BaseCondition = (int)GroupFilterBaseCondition.Include;
                    groupFilter.Conditions = new();
                    groupFilter.SortCriteriaList = new()
                    {
                        new GroupFilterSortingCriteria()
                        {
                            SortType = GroupFilterSorting.GroupFilterName,
                            SortDirection = GroupFilterSortDirection.Asc,
                        },
                    };
                }
                else
                {
                    groupFilter.BaseCondition = (int)(IsInverted ? GroupFilterBaseCondition.Exclude : GroupFilterBaseCondition.Include);
                    if (Conditions != null)
                    {
                        groupFilter.Conditions = Conditions
                            .Select(c => new GroupFilterCondition()
                            {
                                ConditionOperator = (int)c.Operator,
                                ConditionParameter = c.Parameter,
                                ConditionType = (int)c.Type,
                            })
                            .ToList();
                    }
                    if (Sorting != null)
                    {
                        groupFilter.SortCriteriaList = Sorting
                            .Select(s => new GroupFilterSortingCriteria
                            {
                                SortType = s.Type,
                                SortDirection = s.IsInverted ? GroupFilterSortDirection.Desc : GroupFilterSortDirection.Asc
                            })
                            .ToList();
                    }
                }

                // Re-calculate the groups and series that belong to the group filter.
                groupFilter.CalculateGroupsAndSeries();

                // Skip saving if we're just going to preview a group filter.
                if (!skipSave)
                    RepoFactory.GroupFilter.Save(groupFilter);

                return new Filter(ctx, groupFilter, true);
            }
        }
    }
}
