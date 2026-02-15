using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class AverageAniDBRatingSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the average AniDB rating in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AverageAniDBRating;
    }
}
