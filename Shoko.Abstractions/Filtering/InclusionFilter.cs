namespace Shoko.Abstractions.Filtering;

/// <summary>
///   A three-way filter on one condition: leave out what meets it, keep
///   everything, or keep only what meets it. The same three states as the
///   include filters of the REST API.
/// </summary>
public enum InclusionFilter
{
    /// <summary>
    ///   Leave out everything that meets the condition.
    /// </summary>
    False = 0,

    /// <summary>
    ///   Keep everything, whether it meets the condition or not.
    /// </summary>
    True = 1,

    /// <summary>
    ///   Keep only what meets the condition.
    /// </summary>
    Only = 2,
}
