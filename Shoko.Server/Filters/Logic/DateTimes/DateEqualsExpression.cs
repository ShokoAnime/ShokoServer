using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.DateTimes;

public class DateEqualsExpression : FilterExpression<bool>, IWithDateSelectorParameter, IWithSecondDateSelectorParameter, IWithDateParameter
{
    public DateEqualsExpression(FilterExpression<DateTime?> left, FilterExpression<DateTime?> right)
    {
        Left = left;
        Right = right;
    }
    public DateEqualsExpression(FilterExpression<DateTime?> left, DateTime parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public DateEqualsExpression() { }

    public FilterExpression<DateTime?> Left { get; set; }
    public FilterExpression<DateTime?> Right { get; set; }
    public DateTime Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This passes if the left selector equals either the right selector or the parameter";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var date = Left.Evaluate(filterable, userInfo);
        var dateIsNull = date == null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch;
        var operand = Right == null ? Parameter : Right.Evaluate(filterable, userInfo);
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

        return (date > operand ? date - operand : operand - date).Value.TotalDays < 1;
    }

    protected bool Equals(DateEqualsExpression other)
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

        return Equals((DateEqualsExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(DateEqualsExpression left, DateEqualsExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DateEqualsExpression left, DateEqualsExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is DateEqualsExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
