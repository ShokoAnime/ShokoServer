namespace Shoko.Abstractions.Filtering;

/// <summary>
///   Helpers for <see cref="InclusionFilter"/>.
/// </summary>
public static class InclusionFilterExtensions
{
    /// <summary>
    ///   Checks whether something passes the filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="meetsCondition">Whether the thing meets the filter's condition.</param>
    /// <returns><c>true</c> if the filter keeps it, otherwise <c>false</c>.</returns>
    public static bool Passes(this InclusionFilter filter, bool meetsCondition)
        => filter switch
        {
            InclusionFilter.True => true,
            InclusionFilter.Only => meetsCondition,
            _ => !meetsCondition,
        };
}
