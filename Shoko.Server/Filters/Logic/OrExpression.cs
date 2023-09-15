using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic;

public class OrExpression : FilterExpression<bool>, IWithExpressionParameter, IWithSecondExpressionParameter
{
    public OrExpression(FilterExpression<bool> left, FilterExpression<bool> right)
    {
        Left = left;
        Right = right;
    }

    public OrExpression() { }

    public override bool TimeDependent => Left.TimeDependent || Right.TimeDependent;
    public override bool UserDependent => Left.UserDependent || Right.UserDependent;

    public FilterExpression<bool> Left { get; set; }
    public FilterExpression<bool> Right { get; set; }

    public override bool Evaluate(Filterable filterable)
    {
        return Left.Evaluate(filterable) || Right.Evaluate(filterable);
    }

    protected bool Equals(OrExpression other)
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

        return Equals((OrExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right);
    }

    public static bool operator ==(OrExpression left, OrExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(OrExpression left, OrExpression right)
    {
        return !Equals(left, right);
    }
}
