using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class HighestAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override double Evaluate(IFilterable f) => Convert.ToDouble(f.HighestAniDBRating);
}
