using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the given tag on the given source.
/// </summary>
public class HasSourceTagExpression : FilterExpression<bool>, IWithStringParameter, IWithSecondStringParameter
{
    /// <inheritdoc/>
    public HasSourceTagExpression(string parameter, string secondParameter)
        => (Parameter, SecondParameter) = (parameter, secondParameter);

    /// <inheritdoc/>
    public HasSourceTagExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public string SecondParameter { get; set; } = string.Empty;

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the given tag (second parameter) on the given source";

    /// <inheritdoc/>
    public override string HelpParameterName => "Source";

    /// <inheritdoc/>
    public override string HelpSecondParameterName => "Tag";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Source is { } source && !string.IsNullOrEmpty(SecondParameter) && filterable.GetTags(source).Contains(SecondParameter);
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same condition with the same parameters.
    /// </summary>
    /// <param name="other">The condition to compare with.</param>
    /// <returns><c>true</c> if both are equal.</returns>
    protected bool Equals(HasSourceTagExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter && SecondParameter == other.SecondParameter;
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

        return Equals((HasSourceTagExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter, SecondParameter);
    }
}
