using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Sorting.Selectors;

/// <summary>
/// This sorts by the number of suggestions the given source makes for a filterable. Counts what the filterable suggests, not what suggests it.
/// </summary>
public class SourceSuggestionCountSortingSelector : SortingExpression, IWithStringParameter
{
    /// <inheritdoc/>
    public SourceSuggestionCountSortingSelector(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public SourceSuggestionCountSortingSelector() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This sorts by the number of suggestions the given source makes for a filterable";

    /// <inheritdoc/>
    public override object Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source ? filterable.GetSuggestions(source) : 0;
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same sorting selector with the same parameter.
    /// </summary>
    /// <param name="other">The sorting selector to compare with.</param>
    /// <returns><c>true</c> if both are equal.</returns>
    protected bool Equals(SourceSuggestionCountSortingSelector other)
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

        return Equals((SourceSuggestionCountSortingSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
