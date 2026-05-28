using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if any of the values in the left selector overlaps with the parameter
/// </summary>
public class SetOverlapsExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringSetParameter
{
    /// <inheritdoc/>
    public SetOverlapsExpression(FilterExpression<IReadOnlySet<string>> left, IReadOnlySet<string> parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public SetOverlapsExpression()
        => (Left, Parameter) = (null, new HashSet<string>());

    /// <inheritdoc/>
    public FilterExpression<IReadOnlySet<string>>? Left { get; set; }

    /// <inheritdoc/>
    public IReadOnlySet<string> Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the values in the left selector overlaps with the parameter";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time) ?? new HashSet<string>();
        if (left.Count == 0)
            return false;

        return left.Overlaps(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(SetOverlapsExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter.SetEquals(other.Parameter);
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

        return Equals((SetOverlapsExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is SetOverlapsExpression exp && (Left?.IsType(exp.Left) ?? true);
}
