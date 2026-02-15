using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class LastAddedDateSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the last date that any episode was added in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.LastAddedDate;
    }
}
