namespace Shoko.Server.Filters.Info;

public class HasAnimeTypeExpression : FilterExpression<bool>
{
    public HasAnimeTypeExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasAnimeTypeExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.AnimeTypes.Contains(Parameter);
    }
}
