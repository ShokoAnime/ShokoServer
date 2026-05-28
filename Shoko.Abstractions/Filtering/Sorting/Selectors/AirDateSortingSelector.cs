using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the date that a filterable first aired
/// </summary>
public class AirDateSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the date that a filterable first aired";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AirDate?.ToDateTime() ?? DateTime.MaxValue;
    }
}
