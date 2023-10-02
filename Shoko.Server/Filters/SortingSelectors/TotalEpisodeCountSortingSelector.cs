using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class TotalEpisodeCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the total number of episodes in a filterable";

    public override object Evaluate(IFilterable f)
    {
        return f.TotalEpisodeCount;
    }
}
