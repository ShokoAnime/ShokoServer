using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class StringInExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithStringSetParameter
{
    public StringInExpression(FilterExpression<string> left, IReadOnlySet<string> parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public StringInExpression() { }

    public FilterExpression<string> Left { get; set; }
    public IReadOnlySet<string> Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This condition passes if any of the value from the left selector is contained within the parameter set";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Parameter;
        return right.Contains(left);
    }

    protected bool Equals(StringInExpression other)
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

        return Equals((StringInExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(StringInExpression left, StringInExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StringInExpression left, StringInExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is StringInExpression exp && Left.IsType(exp.Left);
    }
}
