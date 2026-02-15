using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class MissingEpisodeCountSortingSelector : SortingExpression
{
    public override string HelpDescription => "This sorts by the number of missing episodes from any release group in a filterable";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.MissingEpisodes;
    }
}
