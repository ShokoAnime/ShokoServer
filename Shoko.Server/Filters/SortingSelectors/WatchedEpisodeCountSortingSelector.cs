using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class WatchedEpisodeCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the number of episodes in a filterable that have been watched by the current user";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedEpisodes;
    }
}
