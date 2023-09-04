using System;

namespace Shoko.Server.Filters.Selectors;

public class HighestUserRatingSelector : UserDependentFilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override double Evaluate(UserDependentFilterable f)
    {
        return Convert.ToDouble(f.HighestUserRating);
    }
}
