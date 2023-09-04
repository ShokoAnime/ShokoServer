namespace Shoko.Server.Filters.Logic;

public class XorExpression : FilterExpression<bool>
{
    public XorExpression(FilterExpression<bool> left, FilterExpression<bool> right)
    {
        Left = left;
        Right = right;
    }

    public XorExpression() { }

    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }

    public override bool Evaluate(Filterable filterable)
    {
        return Left.Evaluate(filterable) ^ Right.Evaluate(filterable);
    }
}
