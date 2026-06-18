using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Services;

#pragma warning disable CS0618
namespace Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;

/// <summary>
/// This condition passes if any of the values in the left selector fuzzy matches either the right selector or the parameter
/// </summary>
public class AnyFuzzyMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    private static IFuzzySearchService? s_service;
    private static IFuzzySearchService Service
        => s_service ??= ISystemService.StaticServices.GetRequiredService<IFuzzySearchService>();

    /// <inheritdoc/>
    public AnyFuzzyMatchesExpression(FilterExpression<IReadOnlySet<string>> left, FilterExpression<string> right)
        => (Left, Right) = (left, right);

    /// <inheritdoc/>
    public AnyFuzzyMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
        => (Left, Parameter) = (left, parameter);

    /// <inheritdoc/>
    public AnyFuzzyMatchesExpression() { }

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
    public override string HelpDescription => "This condition passes if any of the values in the left selector fuzzy matches either the right selector or the parameter";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var left = Left?.Evaluate(filterable, userInfo, time);
        if (left is null)
            return false;

        var right = Parameter ?? Right?.Evaluate(filterable, userInfo, time);
        if (string.IsNullOrEmpty(right))
            return false;

        return Service.FuzzyMatchesAnyName(right, left);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(AnyFuzzyMatchesExpression other)
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

        return Equals((AnyFuzzyMatchesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);

    /// <inheritdoc/>
    public override bool IsType(FilterExpression? expression)
        => expression is AnyFuzzyMatchesExpression exp && (Left?.IsType(exp.Left) ?? true) && (Right?.IsType(exp.Right) ?? true);
}
