using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ParentFilter { get; set; }
    }

    public class FilterCondition
    {
        /// <summary>
        /// Condition Type. What it does.<br/>
        /// This is not the GroupFilterConditionType, but the type of the FilterExpression, with 'Expression' removed.<br/>
        /// ex. And, Or, Not, HasAudioLanguage
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// The first, or left, child expression.<br/>
        /// This might be another logic operator like And, a selector for data like Today's Date, or an expression like HasAudioLanguage.<br/>
        /// Whether this is included depends on the expression.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterCondition? Left { get; set; }

        /// <summary>
        /// The second, or right, child expression.<br/>
        /// This might be another logic operator like And, a selector for data like Today's Date, or an expression like HasAudioLanguage.<br/>
        /// Whether this is included depends on the expression.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterCondition? Right { get; set; }

        /// <summary>
        /// The actual value to compare. Dependent on the expression type.<br/>
        /// Coerced this to string to make things easier.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Parameter { get; set; }

        /// <summary>
        /// The actual value to compare. Dependent on the expression type.<br/>
        /// Very few things have a second parameter. Seasons are one of them<br/>
        /// Coerced this to string to make things easier.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? SecondParameter { get; set; }
    }

    public class FilterExpressionHelp
    {
        /// <summary>
        /// The internal type name of the FilterExpression<br/>
        /// This is what you give the API, not actually the internal type (it is the internal type without the word Expression) 
        /// </summary>
        [Required]
        public string Expression { get; init; }

        /// <summary>
        /// A description of what the expression is doing, comparing, etc
        /// </summary>
        [Required]
        public string Description { get; init; }

        /// <summary>
        /// This is what the expression would be considered for parameters, for example, Air Date is a Date Selector
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public FilterExpressionParameterType Type { get; init; }

        /// <summary>
        /// The parameter type that the <see cref="FilterCondition.Left"/> property requires
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public FilterExpressionParameterType? Left { get; init; }

        /// <summary>
        /// The parameter types that the <see cref="FilterCondition.Right"/> property requires<br/>
        /// If multiple are given, then at least one is required
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public FilterExpressionParameterType? Right { get; init; }

        /// <summary>
        /// The parameter type that the <see cref="FilterCondition.Parameter"/> property requires.<br/>
        /// This will always be a string for simplicity in type safety, but the type is what it expects
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public FilterExpressionParameterType? Parameter { get; init; }

        /// <summary>
        /// This will list the possible parameters, usually with the most common ones first.
        /// </summary>
        public string[]? PossibleParameters { get; init; }

        /// <summary>
        /// This will list the possible parameters, usually with the most common ones first.
        /// </summary>
        public string[]? PossibleSecondParameters { get; init; }

        /// <summary>
        /// The parameter type that the <see cref="FilterCondition.SecondParameter"/> property requires<br/>
        /// This will always be a string for simplicity in type safety, but the type is what it expects
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public FilterExpressionParameterType? SecondParameter { get; init; }

        /// <summary>
        /// Magical Json.Net stuff
        /// </summary>
        public bool ShouldSerializePossibleParameters()
        {
            return PossibleParameters?.Length > 0;
        }

        /// <summary>
        /// Magical Json.Net stuff
        /// </summary>
        public bool ShouldSerializePossibleSecondParameters()
        {
            return PossibleSecondParameters?.Length > 0;
        }

        /// <summary>
        /// The type of the parameter. Expressions return a boolean, Selectors return the type of their name, and the rest are values from the user.<br/>
        /// Dates are in yyyy-MM-dd format<br/>
        /// TimeSpans are in d:HH:mm:ss.ffff format (f is milliseconds)
        /// </summary>
        public enum FilterExpressionParameterType
        {
            Expression,
            DateSelector,
            NumberSelector,
            StringSelector,
            Date,
            Number,
            String,
            TimeSpan
        }
    }

    public class SortingCriteriaHelp
    {
        /// <summary>
        /// The internal type name of the FilterExpression<br/>
        /// This is what you give the API, not actually the internal type (it is the internal type without the word Expression) 
        /// </summary>
        [Required]
        public string Type { get; init; }

        /// <summary>
        /// A description of what the expression is doing, comparing, etc
        /// </summary>
        [Required]
        public string Description { get; init; }
    }

    /// <summary>
    /// Sorting Criteria hold info on how Group Filters sort their items.<br/>
    /// It is in a List to follow an OrderBy().ThenBy().ThenBy(), allowing
    /// consistent results with fallbacks.
    /// </summary>
    public class SortingCriteria
    {
        /// <summary>
        /// The sorting type. What it is sorted on.<br/>
        /// This is not the GroupFilterSorting, but the type of the SortingExpression, with 'Expression' removed.<br/>
        /// ex. And, Or, Not, HasAudioLanguage<br/>
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// The next expression to fall back on when the SortingExpression is equal or invalid, for example, sort by Episode Count descending then by Name
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
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
