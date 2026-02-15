using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class HighestUserRatingSortingSelector : SortingExpression
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the highest user rating in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.HighestUserRating;
    }
}
