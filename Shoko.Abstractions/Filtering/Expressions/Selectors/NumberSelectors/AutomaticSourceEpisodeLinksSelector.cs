using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of automatic episode links to the given source.
/// </summary>
public class AutomaticSourceEpisodeLinksSelector : FilterExpression<double>, IWithStringParameter
{
    /// <inheritdoc/>
    public AutomaticSourceEpisodeLinksSelector(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public AutomaticSourceEpisodeLinksSelector() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of automatic episode links to the given source";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source ? filterable.GetAutomaticEpisodeLinks(source) : 0;
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same selector with the same parameter.
    /// </summary>
    /// <param name="other">The selector to compare with.</param>
    /// <returns><c>true</c> if both are equal.</returns>
    protected bool Equals(AutomaticSourceEpisodeLinksSelector other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((AutomaticSourceEpisodeLinksSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
