using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class SubtitleLanguageCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by how many distinct subtitle languages are present in all of the files in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SubtitleLanguages.Count;
    }
}
