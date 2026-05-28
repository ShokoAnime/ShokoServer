using System;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Logic.Strings;

/// <summary>
/// This condition passes if the left selector matches the regular expression given in the parameter
/// </summary>
public class StringRegexMatchesExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithStringParameter
{
    private Regex? _regex;

    /// <inheritdoc/>
    public StringRegexMatchesExpression(FilterExpression<string> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public StringRegexMatchesExpression() { }

    /// <inheritdoc/>
    public FilterExpression<string>? Left { get; set; }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => Left?.TimeDependent ?? false;

    /// <inheritdoc/>
    public override bool UserDependent => Left?.UserDependent ?? false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the left selector matches the regular expression given in the parameter";

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
        return _regex.IsMatch(left);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(StringRegexMatchesExpression other)
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

        return Equals((StringRegexMatchesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is StringRegexMatchesExpression exp && (Left?.IsType(exp.Left) ?? true);
}
