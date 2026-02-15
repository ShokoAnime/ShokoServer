using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.User;

public class HasUnwatchedEpisodesExpression : FilterExpression<bool>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This condition passes if the current user has any unwatched episodes in the filterable";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.UnwatchedEpisodes > 0;
    }

    protected bool Equals(HasUnwatchedEpisodesExpression other)
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

        return Equals((HasUnwatchedEpisodesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasUnwatchedEpisodesExpression left, HasUnwatchedEpisodesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasUnwatchedEpisodesExpression left, HasUnwatchedEpisodesExpression right)
    {
        return !Equals(left, right);
    }
}
