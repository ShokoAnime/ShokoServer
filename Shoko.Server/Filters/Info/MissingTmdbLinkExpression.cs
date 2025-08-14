using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTmdbLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;

    public override bool UserDependent => false;

    public override string Name => "Missing TMDB Link";

    public override string HelpDescription => "This condition passes if any of the anime should have a TMDB link but does not have one";

    internal static readonly AnimeType[] AnimeTypes =
    [
        AnimeType.Unknown,
        AnimeType.MusicVideo,
        AnimeType.Other,
    ];

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        if (!filterable.AnimeTypes.Except(AnimeTypes).Any())
            return false;

        if (filterable.HasTmdbAutoLinkingDisabled)
            return false;

        return !filterable.HasTmdbLink;
    }

    protected bool Equals(MissingTmdbLinkExpression other)
    {
        return base.Equals(other);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((MissingTmdbLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingTmdbLinkExpression left, MissingTmdbLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingTmdbLinkExpression left, MissingTmdbLinkExpression right)
    {
        return !Equals(left, right);
    }
}
