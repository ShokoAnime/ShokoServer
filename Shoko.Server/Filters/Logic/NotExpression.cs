namespace Shoko.Server.Filters.Logic;

public class NotExpression : FilterExpression<bool>
{
    public NotExpression(FilterExpression<bool> left)
    {
        Left = left;
    }

    public NotExpression() { }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;

    public FilterExpression<bool> Left { get; set; }

    public override bool Evaluate(Filterable filterable)
    {
        return !Left.Evaluate(filterable);
    }
}
