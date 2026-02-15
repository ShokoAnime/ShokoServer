using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class HighestUserRatingSelector : FilterExpression<double>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the highest user rating in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.HighestUserRating;
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
