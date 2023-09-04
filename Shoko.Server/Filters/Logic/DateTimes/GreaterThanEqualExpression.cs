using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class GreaterThanEqualExpression : FilterExpression<bool>
{
    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override bool Evaluate(IFilterable filterable)
    {
        var date = Left.Evaluate(filterable);
        var dateIsNull = date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch;
        var operand = Right == null ? Parameter : Right.Evaluate(filterable);
        var operandIsNull = operand == null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch;
        if (dateIsNull && operandIsNull) return true;
        if (dateIsNull) return false;
        if (operandIsNull) return false;
        return date >= operand;
    }
}
