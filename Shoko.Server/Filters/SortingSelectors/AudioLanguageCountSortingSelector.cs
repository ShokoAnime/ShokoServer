using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class AudioLanguageCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by how many distinct audio languages are present in all of the files in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AudioLanguages.Count;
    }
}
