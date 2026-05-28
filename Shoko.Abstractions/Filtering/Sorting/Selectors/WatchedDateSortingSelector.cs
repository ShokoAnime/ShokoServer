using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the first date that a filterable was watched by the current user
/// </summary>
public class WatchedDateSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool UserDependent => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the first date that a filterable was watched by the current user";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedDate ?? DateTime.MaxValue;
    }
}
