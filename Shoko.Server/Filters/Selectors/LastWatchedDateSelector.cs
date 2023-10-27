using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class LastWatchedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This returns the last date that a filterable was watched by the current user";

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
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
