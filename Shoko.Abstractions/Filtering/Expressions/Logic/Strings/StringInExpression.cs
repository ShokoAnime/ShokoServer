using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if any of the value from the left selector is contained within the parameter set
/// </summary>
public class StringInExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithStringSetParameter
{
    /// <inheritdoc/>
    public StringInExpression(FilterExpression<string> left, IReadOnlySet<string> parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public StringInExpression()
        => Parameter = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

    /// <inheritdoc/>
    public FilterExpression<string>? Left { get; set; }

    /// <inheritdoc/>
    public IReadOnlySet<string> Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the value from the left selector is contained within the parameter set";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        var right = Parameter;
        if (string.IsNullOrEmpty(left) || right is null)
            return false;
        return right.Contains(left);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(StringInExpression other)
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

        return Equals((StringInExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is StringInExpression exp && (Left?.IsType(exp.Left) ?? true);
}
