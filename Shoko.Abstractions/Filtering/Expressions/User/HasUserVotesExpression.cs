using System;

namespace Shoko.Abstractions.Filtering.Expressions.User;

/// <summary>
/// This condition passes if the filterable has a user vote
/// </summary>
public class HasUserVotesExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable has a user vote";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.HasVotes;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasUserVotesExpression other)
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

        return Equals((HasUserVotesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
