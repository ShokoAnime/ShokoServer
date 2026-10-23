using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if the left or the right condition passes, or both.
/// </summary>
/// <remarks>
/// A side that is missing or does not take part (see
/// <see cref="FilterExpression.IsApplicable"/>) is left out, and the other
/// side decides.
/// </remarks>
public class OrExpression : FilterExpression<bool>, IWithExpressionParameter, IWithSecondExpressionParameter
{
    /// <inheritdoc/>
    public OrExpression(FilterExpression<bool> left, FilterExpression<bool> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public OrExpression() { }

    /// <inheritdoc/>
    public FilterExpression<bool>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<bool>? Right { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);

    /// <inheritdoc/>
    public override bool IsApplicable => Left is { IsApplicable: true } || Right is { IsApplicable: true };

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left or the right condition passes, or both. A side that is missing or switched off is left out, and the other side decides";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (Left is not { IsApplicable: true })
            return Right?.Evaluate(filterable, userInfo, time) ?? false;

        if (Right is not { IsApplicable: true })
            return Left.Evaluate(filterable, userInfo, time);

        return Left.Evaluate(filterable, userInfo, time) || Right.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(OrExpression other)
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

        return Equals((OrExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is OrExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
