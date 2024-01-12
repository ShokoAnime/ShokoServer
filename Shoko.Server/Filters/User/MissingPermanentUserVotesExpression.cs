using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.User;

public class MissingPermanentUserVotesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => true;
    public override string HelpDescription => "This condition passes if the filterable is missing a user vote that is of the permanent vote type. This has logic for if the filterable should have a vote";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.MissingPermanentVotes;
    }

    protected bool Equals(MissingPermanentUserVotesExpression other)
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

        return Equals((MissingPermanentUserVotesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingPermanentUserVotesExpression left, MissingPermanentUserVotesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingPermanentUserVotesExpression left, MissingPermanentUserVotesExpression right)
    {
        return !Equals(left, right);
    }
}
