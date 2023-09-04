using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LowestUserRatingSelector : UserDependentFilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override double Evaluate(IUserDependentFilterable f) => Convert.ToDouble(f.LowestUserRating);
}
