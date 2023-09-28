using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Server.API.v3.Models.Common;

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
    /// Indicates the filter should be hidden unless explicitly requested. This will hide the filter from the normal UIs.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Indicates the filter should be applied at the series level.
    /// Filter conditions like like Seasons, Years, Tags, etc only count series individually, rather than by group.
    /// </summary>
    public bool ApplyAtSeriesLevel { get; set; }

    /// <summary>
    /// The FilterExpression tree
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public FilterCondition? Expression { get; set; }

    /// <summary>
    /// The sorting criteria
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public SortingCriteria? Sorting { get; set; }

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
        /// Condition Type. What it does.
        /// This is not the GroupFilterConditionType, but the type of the FilterExpression, with 'Expression' removed.
        /// ex. And, Or, Not, HasAudioLanguage
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// The first, or left, child expression.
        /// This might be another logic operator like And, a selector for data like Today's Date, or an expression like HasAudioLanguage.
        /// Whether this is included depends on the expression.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterCondition? Left { get; set; }

        /// <summary>
        /// The second, or right, child expression.
        /// This might be another logic operator like And, a selector for data like Today's Date, or an expression like HasAudioLanguage.
        /// Whether this is included depends on the expression.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterCondition? Right { get; set; }

        /// <summary>
        /// The actual value to compare. Dependent on the expression type.
        /// Coerced this to string to make things easier.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Parameter { get; set; }

        /// <summary>
        /// The actual value to compare. Dependent on the expression type.
        /// Very few things have a second parameter. Seasons are one of them
        /// Coerced this to string to make things easier.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? SecondParameter { get; set; }
    }

    /// <summary>
    /// Sorting Criteria hold info on how Group Filters sort their items.
    /// It is in a List to follow an OrderBy().ThenBy().ThenBy(), allowing
    /// consistent results with fallbacks.
    /// </summary>
    public class SortingCriteria
    {
        /// <summary>
        /// The sorting type. What it is sorted on.
        /// This is not the GroupFilterSorting, but the type of the SortingExpression, with 'Expression' removed.
        /// ex. And, Or, Not, HasAudioLanguage
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// The next expression to fall back on when the SortingExpression is equal or invalid, for example, sort by Episode Count descending then by Name
        /// </summary>
        public SortingCriteria? Next { get; set; }

        /// <summary>
        /// Assumed Ascending unless this is specified. You must set this if you want highest rating, for example
        /// </summary>
        [Required]
        public bool IsInverted { get; set; }
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
            /// Indicates the filter should be hidden unless explictly requested. This will hide the filter from the normal UIs.
            /// </summary>
            public bool IsHidden { get; set; }

            /// <summary>
            /// Inidcates the filter should be applied at the series level.
            /// Filter conditions like like Seasons, Years, Tags, etc only count series individually, rather than by group.
            /// </summary>
            public bool ApplyAtSeriesLevel { get; set; }

            /// <summary>
            /// The FilterExpression tree
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public FilterCondition? Expression { get; set; }

            /// <summary>
            /// The sorting criteria
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public SortingCriteria? Sorting { get; set; }
        }
    }
}
