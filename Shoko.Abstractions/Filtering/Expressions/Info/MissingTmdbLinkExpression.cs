using System;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTmdbLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Missing TMDB Link";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime should have a TMDB link but does not have one";

    /// <summary>
    /// Anime types excluded from automatic TMDB linking.
    /// </summary>
    public static readonly AnimeType[] AnimeTypes =
    [
        AnimeType.Unknown,
        AnimeType.MusicVideo,
        AnimeType.Other,
    ];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        if (!filterable.AnimeTypes.Except(AnimeTypes).Any())
            return false;

        if (filterable.HasTmdbAutoLinkingDisabled)
            return false;

        return !filterable.HasTmdbLink;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingTmdbLinkExpression other)
    {
        return base.Equals(other);
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

        return Equals((MissingTmdbLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
