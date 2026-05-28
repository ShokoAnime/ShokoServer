using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This returns the number of episodes in a filterable that have not been watched by the current user
/// </summary>
public class UnwatchedEpisodeCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of episodes in a filterable that have not been watched by the current user";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.UnwatchedEpisodes;
    }
}
