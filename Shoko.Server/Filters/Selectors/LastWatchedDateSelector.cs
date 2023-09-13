using System;

namespace Shoko.Server.Filters.Selectors;

public class LastWatchedDateSelector : UserDependentFilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override DateTime? Evaluate(UserDependentFilterable f)
    {
        return f.LastWatchedDate;
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
