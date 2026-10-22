using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if the left expression does not pass, e.g. an inverse
/// </summary>
/// <remarks>
/// With no left expression that takes part, this condition does not take
/// part either (see <see cref="FilterExpression.IsApplicable"/>).
/// </remarks>
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
    public override bool IsApplicable => Left is { IsApplicable: true };

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left expression does not pass, e.g. an inverse";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (Left is not { IsApplicable: true })
            return false;

        return !Left.Evaluate(filterable, userInfo, time);
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
