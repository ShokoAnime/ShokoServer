using System;

namespace Shoko.Server.Filters.SortingSelectors;

public class HighestUserRatingSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override object Evaluate(UserDependentFilterable f)
    {
        return Convert.ToDouble(f.HighestUserRating);
    }
}
