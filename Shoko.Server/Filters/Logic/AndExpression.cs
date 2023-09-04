using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic;

public class AndExpression : FilterExpression<bool>
{
    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;
    public override bool Evaluate(IFilterable filterable) => Left.Evaluate(filterable) && Right.Evaluate(filterable);

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }
}
