using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.User;

public class HasPermanentUserVotesExpression : FilterExpression<bool>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This condition passes if the filterable has a user vote that is of the permanent vote type";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.HasPermanentVotes;
    }

    protected bool Equals(HasPermanentUserVotesExpression other)
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

        return Equals((HasPermanentUserVotesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasPermanentUserVotesExpression left, HasPermanentUserVotesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasPermanentUserVotesExpression left, HasPermanentUserVotesExpression right)
    {
        return !Equals(left, right);
    }
}
