using System;

namespace Shoko.Server.Filters.Logic.Strings;

public class EqualExpression : FilterExpression<bool>
{
    public EqualExpression(FilterExpression<string> left, FilterExpression<string> right)
    {
        Left = left;
        Right = right;
    }
    public EqualExpression(FilterExpression<string> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public EqualExpression() { }

    public FilterExpression<string> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(Filterable filterable)
    {
        var left = Left.Evaluate(filterable);
        var right = Parameter ?? Right?.Evaluate(filterable);
        return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
    }
}
