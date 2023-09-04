using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LowestAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override double Evaluate(IFilterable f) => Convert.ToDouble(f.LowestAniDBRating);
}
