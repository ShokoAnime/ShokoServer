using System;

namespace Shoko.Server.Filters.Logic.Numbers;

public class NotEqualExpression : FilterExpression<bool>
{
    public NotEqualExpression(FilterExpression<double> left, FilterExpression<double> right)
    {
        Left = left;
        Right = right;
    }
    public NotEqualExpression(FilterExpression<double> left, double parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public NotEqualExpression() { }
    
    public FilterExpression<double> Left { get; set; }
    public FilterExpression<double> Right { get; set; }
    public double? Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(Filterable filterable)
    {
        var left = Left.Evaluate(filterable);
        var right = Parameter ?? Right.Evaluate(filterable);
        return Math.Abs(left - right) >= 0.001D;
    }

    protected bool Equals(NotEqualExpression other)
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

        return Equals((NotEqualExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(NotEqualExpression left, NotEqualExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NotEqualExpression left, NotEqualExpression right)
    {
        return !Equals(left, right);
    }
}
