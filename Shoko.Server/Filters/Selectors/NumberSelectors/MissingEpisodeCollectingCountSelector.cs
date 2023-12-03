using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class MissingEpisodeCollectingCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the number of missing episodes in a filterable that are from a release group that is already in the filterable";

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.MissingEpisodesCollecting;
    }

    protected bool Equals(MissingEpisodeCollectingCountSelector other)
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

        return Equals((MissingEpisodeCollectingCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingEpisodeCollectingCountSelector left, MissingEpisodeCollectingCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingEpisodeCollectingCountSelector left, MissingEpisodeCollectingCountSelector right)
    {
        return !Equals(left, right);
    }
}
