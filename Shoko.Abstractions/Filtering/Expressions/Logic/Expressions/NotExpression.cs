using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if the left expression does not pass, e.g. an inverse
/// </summary>
public class NotExpression : FilterExpression<bool>, IWithExpressionParameter
{
    /// <inheritdoc/>
    public NotExpression(FilterExpression<bool> left)
        => Left = left;

    /// <inheritdoc/>
    public NotExpression() { }

    /// <inheritdoc/>
    public FilterExpression<bool>? Left { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left expression does not pass, e.g. an inverse";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        return !left.Value;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(NotExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left);
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

        return Equals((NotExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is NotExpression exp && (Left?.IsType(exp.Left) ?? true);
}
