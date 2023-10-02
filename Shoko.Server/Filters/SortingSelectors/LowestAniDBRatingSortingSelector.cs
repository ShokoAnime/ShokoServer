using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class LowestAniDBRatingSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the lowest AniDB rating in a filterable";

    public override object Evaluate(IFilterable f)
    {
        return Convert.ToDouble(f.LowestAniDBRating);
    }
}
