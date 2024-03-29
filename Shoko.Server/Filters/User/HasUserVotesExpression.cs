using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.User;

public class HasUserVotesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This condition passes if the filterable has a user vote";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.HasVotes;
    }

    protected bool Equals(HasUserVotesExpression other)
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

        return Equals((HasUserVotesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasUserVotesExpression left, HasUserVotesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasUserVotesExpression left, HasUserVotesExpression right)
    {
        return !Equals(left, right);
    }
}
