using System;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class GreaterThanEqualExpression : FilterExpression<bool>
{
    public GreaterThanEqualExpression(FilterExpression<DateTime?> left, FilterExpression<DateTime?> right)
    {
        Left = left;
        Right = right;
    }
    public GreaterThanEqualExpression(FilterExpression<DateTime?> left, DateTime parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public GreaterThanEqualExpression() { }

    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(Filterable filterable)
    {
        var date = Left.Evaluate(filterable);
        var dateIsNull = date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch;
        var operand = Right == null ? Parameter : Right.Evaluate(filterable);
        var operandIsNull = operand == null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch;
        if (dateIsNull && operandIsNull)
        {
            return true;
        }

        if (dateIsNull)
        {
            return false;
        }

        if (operandIsNull)
        {
            return false;
        }

        return date >= operand;
    }

    protected bool Equals(GreaterThanEqualExpression other)
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

        return Equals((GreaterThanEqualExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(GreaterThanEqualExpression left, GreaterThanEqualExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(GreaterThanEqualExpression left, GreaterThanEqualExpression right)
    {
        return !Equals(left, right);
    }
}
