using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.NumberSelectors;

public class SeriesTemporaryVoteCountSelector : FilterExpression<double>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the number of series with a temporary vote set in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override double Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SeriesTemporaryVoteCount;
    }

    protected bool Equals(SeriesTemporaryVoteCountSelector other)
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

        return Equals((SeriesTemporaryVoteCountSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(SeriesTemporaryVoteCountSelector left, SeriesTemporaryVoteCountSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SeriesTemporaryVoteCountSelector left, SeriesTemporaryVoteCountSelector right)
    {
        return !Equals(left, right);
    }
}
