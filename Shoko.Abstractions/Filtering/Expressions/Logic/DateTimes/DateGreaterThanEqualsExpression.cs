using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.DateTimes;

/// <summary>
/// This condition passes if the left selector is greater than or equal to either the right selector or the parameter
/// </summary>
public class DateGreaterThanEqualsExpression : FilterExpression<bool>, IWithDateSelectorParameter, IWithSecondDateSelectorParameter, IWithDateParameter
{
    /// <inheritdoc/>
    public DateGreaterThanEqualsExpression(FilterExpression<DateTime?> left, FilterExpression<DateTime?> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public DateGreaterThanEqualsExpression(FilterExpression<DateTime?> left, DateTime parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public DateGreaterThanEqualsExpression() { }

    /// <inheritdoc/>
    public FilterExpression<DateTime?>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<DateTime?>? Right { get; set; }
    /// <inheritdoc/>
    public DateTime Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);
    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left selector is greater than or equal to either the right selector or the parameter";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var date = Left?.Evaluate(filterable, userInfo, time);
        var dateIsNull = date is null || date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue || date.Value == DateTime.UnixEpoch;
        var operand = Right is null ? Parameter : Right.Evaluate(filterable, userInfo, time);
        var operandIsNull = operand is null || operand.Value == DateTime.MinValue || operand.Value == DateTime.MaxValue || operand.Value == DateTime.UnixEpoch;
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

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(DateGreaterThanEqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Parameter.Equals(other.Parameter);
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

        return Equals((DateGreaterThanEqualsExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is DateGreaterThanEqualsExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
