using System;

namespace Shoko.Server.Filters.Selectors;

public class HighestAniDBRatingSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override double Evaluate(Filterable f)
    {
        return Convert.ToDouble(f.HighestAniDBRating);
    }

    protected bool Equals(HighestAniDBRatingSelector other)
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

        return Equals((HighestAniDBRatingSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HighestAniDBRatingSelector left, HighestAniDBRatingSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HighestAniDBRatingSelector left, HighestAniDBRatingSelector right)
    {
        return !Equals(left, right);
    }
}
