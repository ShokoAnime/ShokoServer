using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
///   This sorts by a filterable's name, excluding common words like A, The, etc. Legacy alias for SortNameSortingSelector.
/// </summary>
// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class SortingNameSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override bool Deprecated => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by a filterable's name, excluding common words like A, The, etc. Legacy alias for SortNameSortingSelector.";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.SortName;
    }
}
