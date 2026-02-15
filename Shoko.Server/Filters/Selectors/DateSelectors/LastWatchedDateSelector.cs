using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class LastWatchedDateSelector : FilterExpression<DateTime?>
{
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the last date that a filterable was watched by the current user";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        return userInfo.LastWatchedDate;
    }

    protected bool Equals(LastWatchedDateSelector other)
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

        return Equals((LastWatchedDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(LastWatchedDateSelector left, LastWatchedDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LastWatchedDateSelector left, LastWatchedDateSelector right)
    {
        return !Equals(left, right);
    }
}
