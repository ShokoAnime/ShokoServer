using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
///     This sorts by the number of missing trailer episodes in a filterable.
/// </summary>
public class MissingTrailersEpisodesCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of missing trailer episodes in a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.MissingEpisodeCounts.Trailers;
    }
}
