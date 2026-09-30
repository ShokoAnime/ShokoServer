using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime has TMDB auto-linking disabled
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="HasSourceAutoLinkingDisabledExpression"/>,
/// which it evaluates with the tmdb source. Kept under its own name so
/// saved filters read the same.
/// </remarks>
public class HasTmdbAutoLinkingDisabledExpression : FilterExpression<bool>
{
    private static readonly HasSourceAutoLinkingDisabledExpression _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string Name => "Has TMDB Auto Linking Disabled";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime has TMDB auto-linking disabled";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbAutoLinkingDisabledExpression other)
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

        return Equals((HasTmdbAutoLinkingDisabledExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
