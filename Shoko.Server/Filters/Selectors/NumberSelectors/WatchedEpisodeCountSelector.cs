using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class WatchedEpisodeCountSelector : FilterExpression<double>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the number of episodes in a filterable that have been watched by the current user";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedEpisodes;
    }

    protected bool Equals(WatchedEpisodeCountSelector other)
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

        return Equals((WatchedEpisodeCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(WatchedEpisodeCountSelector left, WatchedEpisodeCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(WatchedEpisodeCountSelector left, WatchedEpisodeCountSelector right)
    {
        return !Equals(left, right);
    }
}
