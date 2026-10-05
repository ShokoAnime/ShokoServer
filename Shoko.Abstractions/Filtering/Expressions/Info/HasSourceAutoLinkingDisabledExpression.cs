using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if automatic linking to the given source is turned off for any of the anime.
/// </summary>
public class HasSourceAutoLinkingDisabledExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasSourceAutoLinkingDisabledExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasSourceAutoLinkingDisabledExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if automatic linking to the given source is turned off for any of the anime";

    /// <inheritdoc/>
    public override string HelpParameterName => "Source";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source && filterable.AutoLinkingDisabledSources.Contains(source);
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same condition with the same parameter.
    /// </summary>
    /// <param name="other">The condition to compare with.</param>
    /// <returns><c>true</c> if both are equal.</returns>
    protected bool Equals(HasSourceAutoLinkingDisabledExpression other)
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

        return Equals((HasSourceAutoLinkingDisabledExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
