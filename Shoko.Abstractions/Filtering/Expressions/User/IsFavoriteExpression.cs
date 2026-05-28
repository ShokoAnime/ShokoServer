using System;

namespace Shoko.Abstractions.Filtering.Expressions.User;

/// <summary>
/// This condition passes if the filterable is marked as Favorite. This exists for backwards compatibility. Custom Tags are a better way to do this.
/// </summary>
public class IsFavoriteExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable is marked as Favorite. This exists for backwards compatibility. Custom Tags are a better way to do this.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.IsFavorite;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(IsFavoriteExpression other)
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

        return Equals((IsFavoriteExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
