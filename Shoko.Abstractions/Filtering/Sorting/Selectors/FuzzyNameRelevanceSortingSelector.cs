using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Services;

#pragma warning disable CS0618
namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// Sorts by how closely any name of a filterable fuzzy-matches the given query.
/// Lower scores sort first (use ascending order), so best matches appear at the top.
/// Intended to be paired with <see cref="Expressions.Info.HasFuzzyNameExpression"/>
/// using the same query string.
/// </summary>
public class FuzzyNameRelevanceSortingSelector : SortingExpression, IWithStringParameter
{
    private static IFuzzySearchService? s_service;
    private static IFuzzySearchService Service
        => s_service ??= ISystemService.StaticServices.GetRequiredService<IFuzzySearchService>();

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public FuzzyNameRelevanceSortingSelector(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public FuzzyNameRelevanceSortingSelector() { }

    /// <inheritdoc/>
    public override string HelpDescription =>
        "Sorts by how closely any name fuzzy-matches the given query. Best matches sort first (ascending order). Falls back to name sorting when relevance is equal or no query is given. Pair with HasFuzzyName using the same query.";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        var name = filterable.Name;
        if (string.IsNullOrWhiteSpace(Parameter))
            return (true, int.MaxValue, 1.0, int.MaxValue, name);
        var score = Service.FuzzyScoreAnyName(Parameter, filterable.PreferredNames);
        return score is { } s
            ? (s.isNotExact, s.index, s.distance, s.lengthDiff, name)
            : (true, int.MaxValue, 1.0, int.MaxValue, name);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(FuzzyNameRelevanceSortingSelector other)
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

        return Equals((FuzzyNameRelevanceSortingSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
