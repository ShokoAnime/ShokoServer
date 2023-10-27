using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class SeriesCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the number of series in a filterable";

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SeriesCount;
    }

    protected bool Equals(SeriesCountSelector other)
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

        return Equals((SeriesCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SeriesCountSelector left, SeriesCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SeriesCountSelector left, SeriesCountSelector right)
    {
        return !Equals(left, right);
    }
}
