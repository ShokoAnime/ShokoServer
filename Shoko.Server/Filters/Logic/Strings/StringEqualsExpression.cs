using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Strings;

public class StringEqualsExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    public StringEqualsExpression(FilterExpression<string> left, FilterExpression<string> right)
    {
        Left = left;
        Right = right;
    }
    public StringEqualsExpression(FilterExpression<string> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public StringEqualsExpression() { }

    public FilterExpression<string> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This passes if the left selector equals either the right selector or the parameter";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Parameter ?? Right?.Evaluate(filterable, userInfo);
        return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
    }

    protected bool Equals(StringEqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Parameter == other.Parameter;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((StringEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(StringEqualsExpression left, StringEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StringEqualsExpression left, StringEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is StringEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
