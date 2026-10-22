namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string as its second parameter.
/// </summary>
public interface IWithSecondStringParameter
{
    /// <summary>
    /// The second string parameter value, or <c>null</c> when it is not set.
    /// An empty string is set.
    /// </summary>
    string? SecondParameter { get; set; }
}
