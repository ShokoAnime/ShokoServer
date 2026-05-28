using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of missing episodes in a filterable that are from a release group that is already in the filterable
/// </summary>
public class MissingEpisodeCollectingCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of missing episodes in a filterable that are from a release group that is already in the filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.MissingEpisodesCollecting;
    }
}
