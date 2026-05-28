using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Functions;

/// <summary>
/// This subtracts a time-span from a date selector.
/// </summary>
public class DateDiffFunction : FilterExpression<DateTime?>, IWithDateSelectorParameter, IWithTimeSpanParameter
{
    /// <inheritdoc/>
    public DateDiffFunction() { }

    /// <inheritdoc/>
    public DateDiffFunction(FilterExpression<DateTime?> left, TimeSpan parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public FilterExpression<DateTime?>? Left { get; set; }

    /// <inheritdoc/>
    public TimeSpan Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This subtracts a time-span from a date selector.";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var date = Left?.Evaluate(filterable, userInfo, time);
        if (date is null)
            return null;

        return date - Parameter;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(DateDiffFunction other)
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

        return Equals((DateDiffFunction)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is DateDiffFunction exp && (Left?.IsType(exp.Left) ?? true);
}
