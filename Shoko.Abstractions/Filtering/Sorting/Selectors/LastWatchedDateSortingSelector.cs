using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the last date that a filterable was watched by the current user
/// </summary>
public class LastWatchedDateSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the last date that a filterable was watched by the current user";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.LastWatchedDate ?? DateTime.MaxValue;
    }
}
