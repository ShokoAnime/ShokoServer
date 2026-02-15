using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Expressions;

public class EqualsExpression : FilterExpression<bool>, IWithExpressionParameter, IWithSecondExpressionParameter, IWithBoolParameter
{
    public EqualsExpression(FilterExpression<bool> left, FilterExpression<bool> right)
    {
        Left = left;
        Right = right;
    }

    public EqualsExpression(FilterExpression<bool> left, bool parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public EqualsExpression() { }

    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.TimeDependent ?? false);
    public override string HelpDescription => "This condition passes if the left expression is equal to the right expression or the parameter.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }

    public bool Parameter { get; set; }

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        var left = Left.Evaluate(filterable, userInfo, time);
        var right = Right?.Evaluate(filterable, userInfo, time) ?? Parameter;
        return left == right;
    }

    protected bool Equals(EqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right);
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

        return Equals((EqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right);
    }

    public static bool operator ==(EqualsExpression left, EqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(EqualsExpression left, EqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is EqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
