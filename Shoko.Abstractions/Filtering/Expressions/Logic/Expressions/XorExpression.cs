using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if either the left expression or the right expression pass, but not both
/// </summary>
public class XorExpression : FilterExpression<bool>, IWithExpressionParameter, IWithSecondExpressionParameter
{
    /// <inheritdoc/>
    public XorExpression(FilterExpression<bool> left, FilterExpression<bool> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public XorExpression() { }

    /// <inheritdoc/>
    public FilterExpression<bool>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<bool>? Right { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if either the left expression or the right expression pass, but not both";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;


    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        var right = Right?.Evaluate(filterable, userInfo, time);
        if (right is null)
            return false;

        return left.Value ^ right.Value;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(XorExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right);
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

        return Equals((XorExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is XorExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
