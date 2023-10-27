using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Expressions;

public class AndExpression : FilterExpression<bool>, IWithExpressionParameter, IWithSecondExpressionParameter
{
    public AndExpression(FilterExpression<bool> left, FilterExpression<bool> right)
    {
        Left = left;
        Right = right;
    }

    public AndExpression() { }

    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;
    public override string HelpDescription => "This passes if both the left expression and the right expression pass";

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Left.Evaluate(filterable, userInfo) && Right.Evaluate(filterable, userInfo);
    }

    protected bool Equals(AndExpression other)
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

        return Equals((AndExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right);
    }

    public static bool operator ==(AndExpression left, AndExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AndExpression left, AndExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is AndExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
