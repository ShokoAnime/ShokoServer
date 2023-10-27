using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Numbers;

public class NumberNotEqualsExpression : FilterExpression<bool>, IWithNumberSelectorParameter, IWithSecondNumberSelectorParameter, IWithNumberParameter
{
    public NumberNotEqualsExpression(FilterExpression<double> left, FilterExpression<double> right)
    {
        Left = left;
        Right = right;
    }
    public NumberNotEqualsExpression(FilterExpression<double> left, double parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public NumberNotEqualsExpression() { }
    
    public FilterExpression<double> Left { get; set; }
    public FilterExpression<double> Right { get; set; }
    public double Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This passes if the left selector is not equal to either the right selector or the parameter";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Right?.Evaluate(filterable, userInfo) ?? Parameter;
        return Math.Abs(left - right) >= 0.001D;
    }

    protected bool Equals(NumberNotEqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Nullable.Equals(Parameter, other.Parameter);
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

        return Equals((NumberNotEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(NumberNotEqualsExpression left, NumberNotEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NumberNotEqualsExpression left, NumberNotEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is NumberNotEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
