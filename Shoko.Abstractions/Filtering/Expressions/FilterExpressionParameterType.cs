namespace Shoko.Abstractions.Filtering.Expressions;

/// <summary>
///   The type of the parameter. Expressions return a boolean, Selectors
///   return the type of their name, and the rest are values from the user.
/// </summary>
public enum FilterExpressionParameterType
{
    /// <summary>
    /// A boolean filter expression.
    /// </summary>
    Expression,

    /// <summary>
    /// A selector that returns a nullable date.
    /// </summary>
    DateSelector,

    /// <summary>
    /// A selector that returns a number.
    /// </summary>
    NumberSelector,

    /// <summary>
    /// A selector that returns a string.
    /// </summary>
    StringSelector,

    /// <summary>
    /// A selector that returns a set of strings.
    /// </summary>
    StringSetSelector,

    /// <summary>
    /// A date value provided by the user.
    /// </summary>
    Date,

    /// <summary>
    /// A numeric value provided by the user.
    /// </summary>
    Number,

    /// <summary>
    /// A string value provided by the user.
    /// </summary>
    String,

    /// <summary>
    /// A time-span value provided by the user.
    /// </summary>
    TimeSpan,

    /// <summary>
    /// A boolean value provided by the user.
    /// </summary>
    Bool,

    /// <summary>
    /// A set of strings provided by the user.
    /// </summary>
    StringSet,
}
