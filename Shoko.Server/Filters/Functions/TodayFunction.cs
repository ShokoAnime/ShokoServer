using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Functions;

public class TodayFunction : FilterExpression<DateTime?>
{
    public override bool TimeDependent => true;
    public override string HelpDescription => "This returns the current date, at midnight (00:00:00.0000)";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return time.Value!.Date;
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
