using System;

namespace Shoko.Abstractions.Filtering.Expressions.User;

/// <summary>
/// This condition passes if the current user has any unwatched episodes in the filterable
/// </summary>
public class HasUnwatchedEpisodesExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the current user has any unwatched episodes in the filterable";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.UnwatchedEpisodes > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasUnwatchedEpisodesExpression other)
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

        return Equals((HasUnwatchedEpisodesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
