using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class AverageAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the average AniDB rating in a filterable";

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Convert.ToDouble(filterable.AverageAniDBRating);
    }

    protected bool Equals(AverageAniDBRatingSelector other)
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

        return Equals((AverageAniDBRatingSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AverageAniDBRatingSelector left, AverageAniDBRatingSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AverageAniDBRatingSelector left, AverageAniDBRatingSelector right)
    {
        return !Equals(left, right);
    }
}
