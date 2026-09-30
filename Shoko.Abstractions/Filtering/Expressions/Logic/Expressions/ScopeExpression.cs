using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;

/// <summary>
/// This condition passes if the left expression passes. It adds nothing but
/// the parentheses a filter was written with, so a filter language can write
/// the grouping back as it was typed, and an editor can show it as a group
/// with an optional label.
/// </summary>
public class ScopeExpression : FilterExpression<bool>, IWithExpressionParameter, IWithStringParameter
{
    /// <summary>
    /// Creates a scope around an expression.
    /// </summary>
    /// <param name="left">The expression in the parentheses.</param>
    public ScopeExpression(FilterExpression<bool> left)
        => Left = left;

    /// <summary>
    /// Creates a labelled scope around an expression.
    /// </summary>
    /// <param name="left">The expression in the parentheses.</param>
    /// <param name="label">The group's label or comment, or <see langword="null"/> for none.</param>
    public ScopeExpression(FilterExpression<bool> left, string? label)
    {
        Left = left;
        Parameter = label;
    }

    /// <summary>
    /// Creates an empty scope, for deserialization.
    /// </summary>
    public ScopeExpression() { }

    /// <summary>
    /// The expression in the parentheses.
    /// </summary>
    public FilterExpression<bool>? Left { get; set; }

    /// <summary>
    /// The group's label or comment, or <see langword="null"/> for none. It
    /// does not change what the filter matches.
    /// </summary>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left expression passes, e.g. a pair of parentheses";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
        => Left?.Evaluate(filterable, userInfo, time) ?? false;

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(ScopeExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter == other.Parameter;
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

        return Equals((ScopeExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is ScopeExpression exp && (Left?.IsType(exp.Left) ?? true);
}
