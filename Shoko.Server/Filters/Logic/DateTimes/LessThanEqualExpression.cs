using System;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class LessThanEqualExpression : FilterExpression<bool>
{
    public LessThanEqualExpression(FilterExpression<DateTime?> left, FilterExpression<DateTime?> right)
    {
        Left = left;
        Right = right;
    }
    public LessThanEqualExpression(FilterExpression<DateTime?> left, DateTime parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public LessThanEqualExpression() { }
    
    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(Filterable filterable)
    {
        var date = Left.Evaluate(filterable);
        if (date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch)
        {
            return false;
        }

        var operand = Right == null ? Parameter : Right.Evaluate(filterable);
        if (operand == null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch)
        {
            return false;
        }

        return date <= operand;
    }

    protected bool Equals(LessThanEqualExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Parameter.Equals(other.Parameter);
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

        return Equals((LessThanEqualExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(LessThanEqualExpression left, LessThanEqualExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LessThanEqualExpression left, LessThanEqualExpression right)
    {
        return !Equals(left, right);
    }
}
