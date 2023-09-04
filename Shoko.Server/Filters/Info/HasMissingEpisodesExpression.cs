namespace Shoko.Server.Filters.Info;

public class HasMissingEpisodesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.MissingEpisodes > 0;
    }
}
