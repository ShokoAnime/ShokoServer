namespace Shoko.Server.Filters.Info;

public class HasTagExpression : FilterExpression<bool>
{
    public HasTagExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasTagExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.Tags.Contains(Parameter);
    }
}
