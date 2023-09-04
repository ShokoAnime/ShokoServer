namespace Shoko.Server.Filters.Info;

public class InYearExpression : FilterExpression<bool>
{
    public InYearExpression(int parameter)
    {
        Parameter = parameter;
    }
    public InYearExpression() { }

    public int Parameter { get; set; }
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.Years.Contains(Parameter);
    }
}
