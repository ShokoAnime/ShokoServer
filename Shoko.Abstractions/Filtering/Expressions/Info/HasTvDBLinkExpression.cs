using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///   This condition passes if any of the anime have a TvDB link.
/// </summary>
// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class HasTvDBLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have a TvDB link";

    /// <inheritdoc/>
    public override bool Deprecated => true;

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return false;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasTvDBLinkExpression other)
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

        return Equals((HasTvDBLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
