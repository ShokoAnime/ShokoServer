using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class WatchedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the first date that a filterable was watched by the current user";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.WatchedDate;
    }

    protected bool Equals(WatchedDateSelector other)
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

        return Equals((WatchedDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(WatchedDateSelector left, WatchedDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(WatchedDateSelector left, WatchedDateSelector right)
    {
        return !Equals(left, right);
    }
}
