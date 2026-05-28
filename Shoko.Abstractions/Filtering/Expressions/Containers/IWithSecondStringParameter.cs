namespace Shoko.Abstractions.Filtering.Expressions.Containers;

/// <summary>
/// An expression that accepts a string as its second parameter.
/// </summary>
public interface IWithSecondStringParameter
{
    /// <summary>
    /// The second string parameter value.
    /// </summary>
    string SecondParameter { get; set; }
}
