namespace Shoko.Server.Filters.Info;

public class HasMissingEpisodesCollectingExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.MissingEpisodesCollecting > 0;
    }
}
