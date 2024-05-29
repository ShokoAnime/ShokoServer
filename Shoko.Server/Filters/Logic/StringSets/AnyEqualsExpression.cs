using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class AnyEqualsExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    public AnyEqualsExpression(FilterExpression<IReadOnlySet<string>> left, FilterExpression<string> right)
    {
        Left = left;
        Right = right;
    }

    public AnyEqualsExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public AnyEqualsExpression() { }

    public FilterExpression<IReadOnlySet<string>> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This condition passes if any of the values in the left selector equal either the right selector or the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Parameter ?? Right?.Evaluate(filterable, userInfo);
        if (string.IsNullOrEmpty(right)) return !left.Any();
        return left.Any(a => a.Equals(right, StringComparison.InvariantCultureIgnoreCase));
    }

    protected bool Equals(AnyEqualsExpression other)
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

        return Equals((AnyEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(AnyEqualsExpression left, AnyEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AnyEqualsExpression left, AnyEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is AnyEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
