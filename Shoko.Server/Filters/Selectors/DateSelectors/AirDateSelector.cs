using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class AirDateSelector : FilterExpression<DateTime?>
{
    public override string HelpDescription => "This returns the first date that a filterable aired";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.AirDate;
    }

    protected bool Equals(AirDateSelector other)
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

        return Equals((AirDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AirDateSelector left, AirDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AirDateSelector left, AirDateSelector right)
    {
        return !Equals(left, right);
    }
}
