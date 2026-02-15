using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class WatchedDateSelector : FilterExpression<DateTime?>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the first date that a filterable was watched by the current user";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
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
