using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class TotalEpisodeCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the total number of episodes in a filterable";

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.TotalEpisodeCount;
    }

    protected bool Equals(TotalEpisodeCountSelector other)
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

        return Equals((TotalEpisodeCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(TotalEpisodeCountSelector left, TotalEpisodeCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TotalEpisodeCountSelector left, TotalEpisodeCountSelector right)
    {
        return !Equals(left, right);
    }
}
