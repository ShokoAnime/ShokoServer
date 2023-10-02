using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class MissingEpisodeCollectingCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the number of missing episodes in a filterable that are from a release group that is already in the filterable";

    public override object Evaluate(IFilterable f)
    {
        return f.MissingEpisodesCollecting;
    }
}
