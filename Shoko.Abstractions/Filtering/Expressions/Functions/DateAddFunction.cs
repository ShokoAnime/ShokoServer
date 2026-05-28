using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Functions;

/// <summary>
/// This adds a time-span to a date selector
/// </summary>
public class DateAddFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    /// <inheritdoc/>
    public DateAddFunction(FilterExpression<DateTime?> left, TimeSpan parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public DateAddFunction() { }

    /// <inheritdoc/>
    public FilterExpression<DateTime?>? Left { get; set; }

    /// <inheritdoc/>
    public TimeSpan Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This adds a time-span to a date selector";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var date = Left?.Evaluate(filterable, userInfo, time);
        if (date is null)
            return null;

        return date + Parameter;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(DateAddFunction other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter.Equals(other.Parameter);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((DateAddFunction)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is DateAddFunction exp && (Left?.IsType(exp.Left) ?? true);
}
