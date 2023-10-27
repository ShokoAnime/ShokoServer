using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class DateDiffFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    public DateDiffFunction(FilterExpression<DateTime?> selector, TimeSpan parameter)
    {
        Selector = selector;
        Parameter = parameter;
    }
    public DateDiffFunction() { }

    public FilterExpression<DateTime?> Selector { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;
    public override string HelpDescription => "This subtracts a timespan from a date selector.";

    public FilterExpression<DateTime?> Left
    {
        get => Selector;
        set => Selector = value;
    }

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Selector.Evaluate(filterable, userInfo) - Parameter;
    }

    protected bool Equals(DateDiffFunction other)
    {
        return base.Equals(other) && Equals(Selector, other.Selector) && Parameter.Equals(other.Parameter);
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
        return HashCode.Combine(base.GetHashCode(), Selector, Parameter);
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
        return expression is DateDiffFunction exp && Left.IsType(exp.Left) && Selector.IsType(exp.Selector);
    }
}
