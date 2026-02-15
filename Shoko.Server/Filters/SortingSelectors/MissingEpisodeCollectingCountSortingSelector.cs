using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class MissingEpisodeCollectingCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the number of missing episodes in a filterable that are from a release group that is already in the filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.MissingEpisodesCollecting;
    }
}
