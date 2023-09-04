using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class NotEqualExpression : FilterExpression<bool>
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
        if (dateIsNull && operandIsNull) return false;
        if (dateIsNull) return true;
        if (operandIsNull) return true;
        return (date > operand ? date - operand : operand - date).Value.TotalDays >= 1;
    }
}
