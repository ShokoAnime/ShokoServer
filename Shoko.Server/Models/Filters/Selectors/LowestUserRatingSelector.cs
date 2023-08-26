using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Selectors;

public class LowestUserRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override double Evaluate(IFilterable f) => Convert.ToDouble(f.LowestUserRating);
}
