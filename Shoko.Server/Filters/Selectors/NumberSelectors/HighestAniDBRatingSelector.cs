using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class HighestAniDBRatingSelector : FilterExpression<double>
{

    public override string HelpDescription => "This returns the highest AniDB rating in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.HighestAniDBRating;
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
