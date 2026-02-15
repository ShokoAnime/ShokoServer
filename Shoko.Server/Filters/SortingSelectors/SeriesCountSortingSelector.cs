using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class SeriesCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the number of series in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SeriesCount;
    }
}
