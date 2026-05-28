using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if all of the values in the left selector matches the regex provided in the parameter
/// </summary>
public class AllRegexMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringParameter
{
    private Regex? _regex;

    /// <inheritdoc/>
    public AllRegexMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public AllRegexMatchesExpression() { }

    /// <inheritdoc/>
    public FilterExpression<IReadOnlySet<string>>? Left { get; set; }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if all of the values in the left selector matches the regex provided in the parameter";

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
        return left.All(_regex.IsMatch);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AllRegexMatchesExpression other)
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

        return Equals((AllRegexMatchesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is AllRegexMatchesExpression exp && (Left?.IsType(exp.Left) ?? true);
}
