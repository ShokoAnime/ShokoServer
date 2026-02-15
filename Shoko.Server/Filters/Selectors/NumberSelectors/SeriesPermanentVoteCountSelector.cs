using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class SeriesPermanentVoteCountSelector : FilterExpression<double>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the number of series with a permanent vote set in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return userInfo.SeriesPermanentVoteCount;
    }

    protected bool Equals(SeriesPermanentVoteCountSelector other)
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

        return Equals((SeriesPermanentVoteCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SeriesPermanentVoteCountSelector left, SeriesPermanentVoteCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SeriesPermanentVoteCountSelector left, SeriesPermanentVoteCountSelector right)
    {
        return !Equals(left, right);
    }
}
