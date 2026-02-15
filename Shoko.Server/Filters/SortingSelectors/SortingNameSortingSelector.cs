using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class SortingNameSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by a filterable's name, excluding common words like A, The, etc.";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SortingName;
    }
}
