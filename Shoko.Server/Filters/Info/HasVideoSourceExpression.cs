namespace Shoko.Server.Filters.Info;

public class HasVideoSourceExpression : FilterExpression<bool>
{
    public HasVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.VideoSources.Contains(Parameter);
    }
}
