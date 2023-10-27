using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class DateLessThanEqualsExpression : FilterExpression<bool>, IWithDateSelectorParameter, IWithSecondDateSelectorParameter, IWithDateParameter
{
    public DateLessThanEqualsExpression(FilterExpression<DateTime?> left, FilterExpression<DateTime?> right)
    {
        Left = left;
        Right = right;
    }
    public DateLessThanEqualsExpression(FilterExpression<DateTime?> left, DateTime parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public DateLessThanEqualsExpression() { }
    
    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This passes if the left selector is less than or equal to either the right selector or the parameter";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var date = Left.Evaluate(filterable, userInfo);
        if (date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch)
        {
            return false;
        }

        var operand = Right == null ? Parameter : Right.Evaluate(filterable, userInfo);
        if (operand == null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch)
        {
            return false;
        }

        return date <= operand;
    }

    protected bool Equals(DateLessThanEqualsExpression other)
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

        return Equals((DateLessThanEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(DateLessThanEqualsExpression left, DateLessThanEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DateLessThanEqualsExpression left, DateLessThanEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is DateLessThanEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
