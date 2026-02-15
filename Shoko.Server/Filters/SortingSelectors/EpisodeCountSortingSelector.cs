using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class EpisodeCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the total number of episodes in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.EpisodeCount;
    }
}
