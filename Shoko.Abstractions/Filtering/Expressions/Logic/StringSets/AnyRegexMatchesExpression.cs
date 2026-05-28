using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if any of the values in the left selector matches the regex provided in the parameter
/// </summary>
public class AnyRegexMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringParameter
{
    private Regex? _regex;
    /// <inheritdoc/>
    public AnyRegexMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public AnyRegexMatchesExpression()
        => (Left, Parameter) = (null, null);

    /// <inheritdoc/>
    public FilterExpression<IReadOnlySet<string>>? Left { get; set; }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the values in the left selector matches the regex provided in the parameter";
    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        if (Parameter is null)
            return false;

        _regex ??= new Regex(Parameter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return left.Any(_regex.IsMatch);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AnyRegexMatchesExpression other)
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

        return Equals((AnyRegexMatchesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is AnyRegexMatchesExpression exp && (Left?.IsType(exp.Left) ?? true);
}
