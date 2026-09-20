using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of suggestions all the sources make for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class TotalSuggestionCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of suggestions all the sources make for a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.TotalSuggestions;
    }
}
