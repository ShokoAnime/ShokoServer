using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.User;

public class HasWatchedEpisodesExpression : FilterExpression<bool>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This condition passes if the current user has any watched episodes in the filterable";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedEpisodes > 0;
    }

    protected bool Equals(HasWatchedEpisodesExpression other)
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

        return Equals((HasWatchedEpisodesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasWatchedEpisodesExpression left, HasWatchedEpisodesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasWatchedEpisodesExpression left, HasWatchedEpisodesExpression right)
    {
        return !Equals(left, right);
    }
}
