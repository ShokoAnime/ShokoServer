using System;
using System.Collections.Generic;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class SetOverlapsExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringSetParameter
{
    public SetOverlapsExpression(FilterExpression<IReadOnlySet<string>> left, IReadOnlySet<string> parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public SetOverlapsExpression() { }

    public FilterExpression<IReadOnlySet<string>> Left { get; set; }
    public IReadOnlySet<string> Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This condition passes if any of the values in the left selector overlaps with the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Parameter;
        return left.Overlaps(right);
    }

    protected bool Equals(SetOverlapsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter.SetEquals(other.Parameter);
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

        return Equals((SetOverlapsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(SetOverlapsExpression left, SetOverlapsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SetOverlapsExpression left, SetOverlapsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is SetOverlapsExpression exp && Left.IsType(exp.Left);
    }
}
