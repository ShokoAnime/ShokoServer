using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime should be linked to the given source but are not.
/// </summary>
/// <remarks>
/// An anime linked to nothing on the source on purpose, or told to leave the
/// source alone, is not missing a link, nor is one whose type the source is
/// never linked for (<see cref="AnimeTypes"/>).
/// </remarks>
public class MissingSourceLinkExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public MissingSourceLinkExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public MissingSourceLinkExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The source asked about, when the parameter names one.
    /// </summary>
    protected MetadataSource? Source
        => MetadataSource.TryParse(Parameter, out var source) ? source : null;

    /// <summary>
    /// The anime types that are never expected to have a link.
    /// </summary>
    public static readonly AnimeType[] AnimeTypes =
    [
        AnimeType.Unknown,
        AnimeType.MusicVideo,
        AnimeType.Other,
    ];

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime should be linked to the given source but are not";

    /// <inheritdoc/>
    public override string HelpParameterName => "Source";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (Source is not { } source)
            return false;

        if (!filterable.AnimeTypes.Except(AnimeTypes).Any())
            return false;

        if (filterable.AutoLinkingDisabledSources.Contains(source))
            return false;

        return !filterable.LinkedSources.Contains(source) && !filterable.UnlinkedSources.Contains(source);
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same condition with the same parameter.
    /// </summary>
    /// <param name="other">The condition to compare with.</param>
    /// <returns><c>true</c> if both are equal.</returns>
    protected bool Equals(MissingSourceLinkExpression other)
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

        return Equals((MissingSourceLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }
}
