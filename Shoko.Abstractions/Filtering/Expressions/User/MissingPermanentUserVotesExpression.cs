using System;

namespace Shoko.Abstractions.Filtering.Expressions.User;

/// <summary>
/// This condition passes if the filterable is missing a user vote that is of the permanent vote type. This has logic for if the filterable should have a vote
/// </summary>
public class MissingPermanentUserVotesExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool TimeDependent => true;
    /// <inheritdoc/>
    public override bool UserDependent => true;
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the filterable is missing a user vote that is of the permanent vote type. This has logic for if the filterable should have a vote";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.MissingPermanentVotes;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingPermanentUserVotesExpression other)
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

        return Equals((MissingPermanentUserVotesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
