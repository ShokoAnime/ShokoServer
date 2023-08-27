using System;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.Logic.DateTimes;

public class LessThanEqualExpression : FilterExpression<bool>
{
    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override bool Evaluate(IFilterable filterable)
    {
        var date = Left.Evaluate(filterable);
        if (date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch) return false;
        var operand = Right == null ? Parameter : Right.Evaluate(filterable);
        if (operand == null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch) return false;
        return date <= operand;
    }
}
