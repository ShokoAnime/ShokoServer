using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime has AniList auto-linking disabled
/// </summary>
public class HasAnilistAutoLinkingDisabledExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string Name => "Has AniList Auto Linking Disabled";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime has AniList auto-linking disabled";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.HasAnilistAutoLinkingDisabled;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnilistAutoLinkingDisabledExpression other)
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

        return Equals((HasAnilistAutoLinkingDisabledExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
