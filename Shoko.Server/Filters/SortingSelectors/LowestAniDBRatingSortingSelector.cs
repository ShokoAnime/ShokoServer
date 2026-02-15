using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class LowestAniDBRatingSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the lowest AniDB rating in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.LowestAniDBRating;
    }
}
