using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have an AniList link
/// </summary>
public class HasAnilistLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Has AniList Link";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have an AniList link";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.HasAnilistLink;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnilistLinkExpression other)
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

        return Equals((HasAnilistLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
