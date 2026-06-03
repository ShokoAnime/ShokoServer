using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of watched other episodes in a filterable
/// </summary>
public class WatchedOthersEpisodesCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of watched other episodes in a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return userInfo?.WatchedEpisodeCounts.Others ?? 0;
    }
}
