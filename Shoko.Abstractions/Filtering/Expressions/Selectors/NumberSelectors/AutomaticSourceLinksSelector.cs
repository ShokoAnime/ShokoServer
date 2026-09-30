using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;

/// <summary>
/// This returns the number of series and movie-level links to the given source
/// that no user verified.
/// </summary>
/// <remarks>
/// Counts the links behind <see cref="IFilterableInfo.LinkedSources"/> and
/// <see cref="IFilterableInfo.UnlinkedSources"/>, so a link made to nothing
/// counts. Episode links are counted by the episode link selectors.
/// </remarks>
public class AutomaticSourceLinksSelector : FilterExpression<double>, IWithStringParameter
{
    /// <summary>
    /// Creates the expression for a source.
    /// </summary>
    /// <param name="parameter">The source, by its value, an alias or an old spelling.</param>
    public AutomaticSourceLinksSelector(string parameter)
        => Parameter = parameter;

    /// <summary>
    /// Creates the expression with no source, which a saved filter fills in.
    /// </summary>
    public AutomaticSourceLinksSelector() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the number of series and movie-level links to the given source that no user verified";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source ? filterable.GetAutomaticLinks(source) : 0;
    }

    /// <summary>
    /// Checks whether another expression asks about the same source.
    /// </summary>
    /// <param name="other">The other expression.</param>
    /// <returns><c>true</c> when both ask about the same source.</returns>
    protected bool Equals(AutomaticSourceLinksSelector other)
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

        return Equals((AutomaticSourceLinksSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
