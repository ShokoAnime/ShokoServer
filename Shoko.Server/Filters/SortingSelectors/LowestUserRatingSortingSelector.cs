using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LowestUserRatingSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the lowest user rating in a filterable";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return Convert.ToDouble(userInfo.LowestUserRating);
    }
}
