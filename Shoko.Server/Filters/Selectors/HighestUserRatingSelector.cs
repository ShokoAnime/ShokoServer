using System;

namespace Shoko.Server.Filters.Selectors;

public class HighestUserRatingSelector : UserDependentFilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override double Evaluate(UserDependentFilterable f)
    {
        return Convert.ToDouble(f.HighestUserRating);
    }

    protected bool Equals(HighestUserRatingSelector other)
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

        return Equals((HighestUserRatingSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HighestUserRatingSelector left, HighestUserRatingSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HighestUserRatingSelector left, HighestUserRatingSelector right)
    {
        return !Equals(left, right);
    }
}
