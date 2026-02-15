using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.SortingSelectors;

public class UnwatchedEpisodeCountSortingSelector : SortingExpression
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the number of episodes in a filterable that have not been watched by the current user";

    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.UnwatchedEpisodes;
    }
}
