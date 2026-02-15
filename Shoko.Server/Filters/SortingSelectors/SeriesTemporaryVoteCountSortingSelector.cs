using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class SeriesTemporaryVoteCountSortingSelector : SortingExpression
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the number of series with a temporary vote set in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return userInfo.SeriesTemporaryVoteCount;
    }
}
