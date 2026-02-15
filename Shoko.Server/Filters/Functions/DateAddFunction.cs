using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class DateAddFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    public DateAddFunction()
    {
    }

    public DateAddFunction(FilterExpression<DateTime?> left, TimeSpan parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public FilterExpression<DateTime?> Left { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This adds a timespan to a date selector";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return Left.Evaluate(filterable, userInfo, time) + Parameter;
    }

    protected bool Equals(DateAddFunction other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter.Equals(other.Parameter);
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

        return Equals((DateAddFunction)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(DateAddFunction left, DateAddFunction right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DateAddFunction left, DateAddFunction right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is DateAddFunction exp && Left.IsType(exp.Left);
    }
}
