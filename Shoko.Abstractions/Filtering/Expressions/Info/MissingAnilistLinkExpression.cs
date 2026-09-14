using System;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime should have an AniList link but does not have one
/// </summary>
public class MissingAnilistLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Missing AniList Link";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime should have an AniList link but does not have one";

    /// <summary>
    /// Anime types excluded from automatic AniList linking.
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

        if (filterable.HasAnilistAutoLinkingDisabled)
            return false;

        return !filterable.HasAnilistLink;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingAnilistLinkExpression other)
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

        return Equals((MissingAnilistLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
