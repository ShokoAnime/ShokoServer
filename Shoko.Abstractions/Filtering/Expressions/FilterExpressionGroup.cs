using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Filtering.Expressions;

/// <summary>
/// Categories used to group filter expressions for UI organization.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum FilterExpressionGroup
{
    /// <summary>
    /// Expressions that check properties or metadata of the filterable.
    /// </summary>
    Info,

    /// <summary>
    /// Logical operators that combine or negate other expressions.
    /// </summary>
    Logic,

    /// <summary>
    /// Functions that transform or compute values from selectors.
    /// </summary>
    Function,

    /// <summary>
    /// Selectors that extract values from the filterable.
    /// </summary>
    Selector,
}
