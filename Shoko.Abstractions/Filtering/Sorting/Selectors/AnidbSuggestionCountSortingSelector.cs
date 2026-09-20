using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of suggestions AniDB makes for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class AnidbSuggestionCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of suggestions AniDB makes for a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.AnidbSuggestions;
    }
}
