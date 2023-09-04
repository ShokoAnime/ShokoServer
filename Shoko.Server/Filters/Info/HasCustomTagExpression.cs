namespace Shoko.Server.Filters.Info;

public class HasCustomTagExpression : FilterExpression<bool>
{
    public HasCustomTagExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasCustomTagExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.CustomTags.Contains(Parameter);
    }
}
