using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the given source suggests anything for the filterable. Looks at what the filterable suggests, not at what suggests it.
/// </summary>
public class HasSourceSuggestionExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasSourceSuggestionExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasSourceSuggestionExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the given source suggests anything for the filterable";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source && filterable.GetSuggestions(source) is > 0;
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same condition with the same parameter.
    /// </summary>
    /// <param name="other">The condition to compare with.</param>
    /// <returns><see langword="true"/> if both are equal.</returns>
    protected bool Equals(HasSourceSuggestionExpression other)
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

        return Equals((HasSourceSuggestionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
