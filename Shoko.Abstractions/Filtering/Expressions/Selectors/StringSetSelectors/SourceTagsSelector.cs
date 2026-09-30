using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;

/// <summary>
/// This returns every tag the given source gives a filterable.
/// </summary>
public class SourceTagsSelector : FilterExpression<IReadOnlySet<string>>, IWithStringParameter
{
    /// <inheritdoc/>
    public SourceTagsSelector(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public SourceTagsSelector() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns a set of all the tags the given source gives a filterable.";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    /// <inheritdoc/>
    public override IReadOnlySet<string> Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source ? filterable.GetTags(source) : new HashSet<string>();
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same selector with the same parameter.
    /// </summary>
    /// <param name="other">The selector to compare with.</param>
    /// <returns><see langword="true"/> if both are equal.</returns>
    protected bool Equals(SourceTagsSelector other)
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

        return Equals((SourceTagsSelector)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
