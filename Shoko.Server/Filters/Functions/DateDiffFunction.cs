using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class DateDiffFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    public DateDiffFunction(FilterExpression<DateTime?> left, TimeSpan parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public DateDiffFunction() { }

    public FilterExpression<DateTime?> Left { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This subtracts a timespan from a date selector.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return Left.Evaluate(filterable, userInfo, time) - Parameter;
    }

    protected bool Equals(DateDiffFunction other)
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

        return Equals((DateDiffFunction)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(DateDiffFunction left, DateDiffFunction right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DateDiffFunction left, DateDiffFunction right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is DateDiffFunction exp && Left.IsType(exp.Left);
    }
}
