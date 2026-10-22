namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a boolean parameter.
/// </summary>
public interface IWithBoolParameter
{
    /// <summary>
    /// The boolean parameter value, or <c>null</c> when it is not set.
    /// <c>false</c> is set.
    /// </summary>
    bool? Parameter { get; set; }
}
