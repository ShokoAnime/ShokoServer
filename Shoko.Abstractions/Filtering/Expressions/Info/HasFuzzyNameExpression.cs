using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Services;

#pragma warning disable CS0618
namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any name of the series or group fuzzy-matches the
/// given query. Matching is proportional-error-tolerant for Latin input and
/// falls back to a substring check for non-Latin (CJK, Arabic, Cyrillic, etc.)
/// input.
/// </summary>
public class HasFuzzyNameExpression : FilterExpression<bool>, IWithStringParameter
{
    private static IFuzzySearchService? s_service;
    private static IFuzzySearchService Service
        => s_service ??= ISystemService.StaticServices.GetRequiredService<IFuzzySearchService>();

    /// <inheritdoc/>
    public HasFuzzyNameExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasFuzzyNameExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription =>
        "This condition passes if any name of the series or group fuzzy-matches the given query.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return false;
        return Service.FuzzyMatchesAnyName(Parameter, filterable.Names);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasFuzzyNameExpression other)
        => base.Equals(other) && Parameter == other.Parameter;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((HasFuzzyNameExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
