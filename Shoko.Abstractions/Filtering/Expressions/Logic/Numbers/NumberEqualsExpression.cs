using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Numbers;

/// <summary>
/// This condition passes if the left selector equals either the right selector or the parameter
/// </summary>
public class NumberEqualsExpression : FilterExpression<bool>, IWithNumberSelectorParameter, IWithSecondNumberSelectorParameter, IWithNumberParameter
{
    /// <inheritdoc/>
    public NumberEqualsExpression(FilterExpression<double> left, FilterExpression<double> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public NumberEqualsExpression(FilterExpression<double> left, double parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public NumberEqualsExpression() { }

    /// <inheritdoc/>
    public FilterExpression<double>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<double>? Right { get; set; }

    /// <inheritdoc/>
    public double Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left selector equals either the right selector or the parameter";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        var right = Right?.Evaluate(filterable, userInfo, time) ?? Parameter;
        return Math.Abs(left.Value - right) < 0.001D;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(NumberEqualsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Equals(Parameter, other.Parameter);
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

        return Equals((NumberEqualsExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is NumberEqualsExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
