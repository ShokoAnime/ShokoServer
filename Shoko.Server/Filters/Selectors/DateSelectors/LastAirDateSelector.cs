using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class LastAirDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the last date that a filterable aired";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.LastAirDate;
    }

    protected bool Equals(LastAirDateSelector other)
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

        return Equals((LastAirDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(LastAirDateSelector left, LastAirDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LastAirDateSelector left, LastAirDateSelector right)
    {
        return !Equals(left, right);
    }
}
