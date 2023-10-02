using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class WatchedEpisodeCountSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the number of episodes in a filterable that have been watched by the current user";

    public override object Evaluate(IUserDependentFilterable f)
    {
        return f.WatchedEpisodes;
    }
}
