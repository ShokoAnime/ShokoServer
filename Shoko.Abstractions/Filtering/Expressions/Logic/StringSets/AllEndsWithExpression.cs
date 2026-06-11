using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if all of the values in the left selector end with either the right selector or the parameter
/// </summary>
public class AllEndsWithExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    /// <inheritdoc/>
    public AllEndsWithExpression(FilterExpression<IReadOnlySet<string>> left, FilterExpression<string> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public AllEndsWithExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public AllEndsWithExpression() { }

    /// <inheritdoc/>
    public FilterExpression<IReadOnlySet<string>>? Left { get; set; }

    /// <inheritdoc/>
    public FilterExpression<string>? Right { get; set; }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => (Left?.TimeDependent ?? false) || (Right?.TimeDependent ?? false);

    /// <inheritdoc/>
    public override bool UserDependent => (Left?.UserDependent ?? false) || (Right?.UserDependent ?? false);

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if all of the values in the left selector end with either the right selector or the parameter";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        var right = Parameter ?? Right?.Evaluate(filterable, userInfo, time);
        if (right is null)
            return false;

        return left.All(a => a.EndsWith(right, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AllEndsWithExpression other)
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

        return Equals((AllEndsWithExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is AllEndsWithExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
