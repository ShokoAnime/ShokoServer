using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Strings;

public class NotEqualExpression : FilterExpression<bool>
{
    public FilterExpression<string> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(IFilterable filterable)
    {
        var left = Left.Evaluate(filterable);
        var right = Parameter ?? Right?.Evaluate(filterable);
        return !string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
    }
}
