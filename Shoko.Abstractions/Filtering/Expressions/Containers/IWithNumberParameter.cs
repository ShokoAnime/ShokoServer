namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a numeric parameter.
/// </summary>
public interface IWithNumberParameter
{
    /// <summary>
    /// The numeric parameter value, or <c>null</c> when it is not set. Any
    /// number, <c>0</c> included, is set.
    /// </summary>
    double? Parameter { get; set; }
}
