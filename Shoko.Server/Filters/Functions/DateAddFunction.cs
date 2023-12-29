using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class DateAddFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    public DateAddFunction()
    {
    }

    public DateAddFunction(FilterExpression<DateTime?> selector, TimeSpan parameter)
    {
        Selector = selector;
        Parameter = parameter;
    }

    public FilterExpression<DateTime?> Selector { get; set; }
    public TimeSpan Parameter { get; set; }

    public override bool TimeDependent => Selector.TimeDependent;
    public override bool UserDependent => Selector.UserDependent;
    public override string HelpDescription => "This adds a timespan to a date selector";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    public FilterExpression<DateTime?> Left
    {
        get => Selector;
        set => Selector = value;
    }

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Selector.Evaluate(filterable, userInfo) + Parameter;
    }

    protected bool Equals(DateAddFunction other)
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

        return Equals((DateAddFunction)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Selector, Parameter);
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
        return expression is DateAddFunction exp && Left.IsType(exp.Left) && Selector.IsType(exp.Selector);
    }
}
