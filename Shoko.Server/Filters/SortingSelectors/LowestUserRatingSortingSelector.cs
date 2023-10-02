using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LowestUserRatingSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This sorts by the lowest user rating in a filterable";

    public override object Evaluate(IUserDependentFilterable f)
    {
        return Convert.ToDouble(f.LowestUserRating);
    }
}
