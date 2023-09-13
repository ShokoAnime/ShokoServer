using System;

namespace Shoko.Server.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override DateTime? Evaluate(Filterable f)
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
