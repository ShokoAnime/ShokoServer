using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Strings;

/// <summary>
/// This condition passes if the left selector is not equal to either the right selector or the parameter
/// </summary>
public class StringNotEqualsExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    /// <inheritdoc/>
    public StringNotEqualsExpression(FilterExpression<string> left, FilterExpression<string> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public StringNotEqualsExpression(FilterExpression<string> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public StringNotEqualsExpression() { }

    /// <inheritdoc/>
    public FilterExpression<string>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<string>? Right { get; set; }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left selector is not equal to either the right selector or the parameter";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        var right = Parameter ?? Right?.Evaluate(filterable, userInfo, time);
        return !string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(StringNotEqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Parameter == other.Parameter;
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

        return Equals((StringNotEqualsExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is StringNotEqualsExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
