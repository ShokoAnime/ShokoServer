using System;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by how many distinct subtitle languages are present in all of the files in a filterable
/// </summary>
public class SubtitleLanguageCountSortingSelector : SortingExpression
{
    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by how many distinct subtitle languages are present in all of the files in a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.SubtitleLanguages.Count;
    }
}
