using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the current date, at midnight (00:00:00.0000)";

    public override DateTime? Evaluate(IFilterable f)
    {
        return DateTime.Today;
    }

    protected bool Equals(TodayFunction other)
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

        return Equals((TodayFunction)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(TodayFunction left, TodayFunction right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(TodayFunction left, TodayFunction right)
    {
        return !Equals(left, right);
    }
}
