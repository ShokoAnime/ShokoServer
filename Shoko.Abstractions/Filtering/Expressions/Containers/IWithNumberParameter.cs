namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a numeric parameter.
/// </summary>
public interface IWithNumberParameter
{
    /// <summary>
    /// The numeric parameter value.
    /// </summary>
    double Parameter { get; set; }
}
