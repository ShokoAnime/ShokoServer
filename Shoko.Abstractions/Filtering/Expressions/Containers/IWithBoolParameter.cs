namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a boolean parameter.
/// </summary>
public interface IWithBoolParameter
{
    /// <summary>
    /// The boolean parameter value.
    /// </summary>
    bool Parameter { get; set; }
}
