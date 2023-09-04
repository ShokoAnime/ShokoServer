namespace Shoko.Server.Filters.SortingSelectors;

public class MissingEpisodeCollectingCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override object Evaluate(Filterable f)
    {
        return f.MissingEpisodesCollecting;
    }
}
