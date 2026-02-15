using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class AddedDateSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the date that a filterable was created";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AddedDate;
    }
}
