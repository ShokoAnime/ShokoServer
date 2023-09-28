using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class EpisodeCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override double Evaluate(IFilterable f)
    {
        return f.EpisodeCount;
    }

    protected bool Equals(EpisodeCountSelector other)
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

        return Equals((EpisodeCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(EpisodeCountSelector left, EpisodeCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(EpisodeCountSelector left, EpisodeCountSelector right)
    {
        return !Equals(left, right);
    }
}
