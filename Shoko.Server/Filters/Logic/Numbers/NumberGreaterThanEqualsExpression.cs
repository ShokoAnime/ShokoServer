using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Numbers;

public class NumberGreaterThanEqualsExpression : FilterExpression<bool>, IWithNumberSelectorParameter, IWithSecondNumberSelectorParameter, IWithNumberParameter
{
    public NumberGreaterThanEqualsExpression(FilterExpression<double> left, FilterExpression<double> right)
    {
        Left = left;
        Right = right;
    }
    public NumberGreaterThanEqualsExpression(FilterExpression<double> left, double parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public NumberGreaterThanEqualsExpression() { }

    public FilterExpression<double> Left { get; set; }
    public FilterExpression<double> Right { get; set; }
    public double Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This condition passes if the left selector is greater than or equal to either the right selector or the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        var left = Left.Evaluate(filterable, userInfo, time);
        var right = Right?.Evaluate(filterable, userInfo, time) ?? Parameter;
        return Math.Abs(left - right) < 0.001D || left > right;
    }

    protected bool Equals(NumberGreaterThanEqualsExpression other)
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

        return Equals((NumberGreaterThanEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(NumberGreaterThanEqualsExpression left, NumberGreaterThanEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NumberGreaterThanEqualsExpression left, NumberGreaterThanEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is NumberGreaterThanEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
