using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic;

public class XorExpression : FilterExpression<bool>
{
    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;
    public override bool Evaluate(IFilterable filterable) => Left.Evaluate(filterable) ^ Right.Evaluate(filterable);

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }
}
