namespace Shoko.Server.Filters.Logic;

public class OrExpression : FilterExpression<bool>
{
    public OrExpression(FilterExpression<bool> left, FilterExpression<bool> right)
    {
        Left = left;
        Right = right;
    }

    public OrExpression() { }

    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }

    public override bool Evaluate(Filterable filterable)
    {
        return Left.Evaluate(filterable) || Right.Evaluate(filterable);
    }
}
