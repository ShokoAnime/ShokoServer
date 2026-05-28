namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string parameter.
/// </summary>
public interface IWithStringParameter
{
    /// <summary>
    /// The string parameter value.
    /// </summary>
    string? Parameter { get; set; }
}
