using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class MissingEpisodeCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the number of missing episodes from any release group in a filterable";

    public override object Evaluate(IFilterable f)
    {
        return f.MissingEpisodes;
    }
}
