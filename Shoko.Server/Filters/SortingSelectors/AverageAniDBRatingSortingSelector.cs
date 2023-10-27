using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class AverageAniDBRatingSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the average AniDB rating in a filterable";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Convert.ToDouble(filterable.AverageAniDBRating);
    }
}
