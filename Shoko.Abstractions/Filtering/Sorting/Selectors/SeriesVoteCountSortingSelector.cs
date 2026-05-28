using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of series with any vote set in a filterable
/// </summary>
public class SeriesVoteCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of series with any vote set in a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.SeriesVoteCount;
    }
}
