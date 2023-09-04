using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Numbers;

public class EqualExpression : FilterExpression<bool>
{
    public FilterExpression<double> Left { get; set; }
    public FilterExpression<double> Right { get; set; }
    public double? Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(IFilterable filterable)
    {
        var left = Left.Evaluate(filterable);
        var right = Parameter ?? Right.Evaluate(filterable);
        return Math.Abs(left - right) < 0.001D;
    }
}
