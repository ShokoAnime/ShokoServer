using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of suggestions a filterable makes that point at a series in the collection. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class LocalSuggestionCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of suggestions a filterable makes that point at a series in the collection";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.LocalSuggestions;
    }
}
