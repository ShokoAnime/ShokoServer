using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class UnwatchedCountSortingSelector : UserDependentSortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override object Evaluate(IUserDependentFilterable f)
    {
        return f.UnwatchedEpisodes;
    }
}
