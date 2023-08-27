using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class HighestUserRatingSelector : UserDependentFilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override double Evaluate(IUserDependentFilterable f) => Convert.ToDouble(f.HighestUserRating);
}
