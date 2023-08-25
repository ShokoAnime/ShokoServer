namespace Shoko.Server.Models.Filters.Logic;

public class XorExpression : FilterExpression
{
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;
    public override bool Evaluate(IFilterable filterable) => Left.Evaluate(filterable) ^ Right.Evaluate(filterable);

    public FilterExpression Left { get; set; }
    public FilterExpression Right { get; set; }
}
