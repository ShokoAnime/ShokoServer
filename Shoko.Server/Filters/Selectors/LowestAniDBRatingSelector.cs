using System;

namespace Shoko.Server.Filters.Selectors;

public class LowestAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override double Evaluate(Filterable f)
    {
        return Convert.ToDouble(f.LowestAniDBRating);
    }
}
