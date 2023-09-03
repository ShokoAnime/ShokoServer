using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class LowestAniDBRatingSortingSelector : SortingExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override double Evaluate(IFilterable f) => Convert.ToDouble(f.LowestAniDBRating);
}
