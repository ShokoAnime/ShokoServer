using System;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have a TMDB link
/// </summary>
/// <remarks>
/// The TMDB form of <see cref="HasSourceLinkExpression"/>, which it
/// evaluates with the tmdb source. Kept under its own name so saved filters
/// read the same.
/// </remarks>
public class HasTmdbLinkExpression : FilterExpression<bool>
{
    private static readonly HasSourceLinkExpression _tmdb = new(MetadataSource.TMDB.Value);

    /// <inheritdoc/>
    public override string Name => "Has TMDB Link";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have a TMDB link";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return _tmdb.Evaluate(filterable, userInfo, time);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTmdbLinkExpression other)
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

        return Equals((HasTmdbLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
