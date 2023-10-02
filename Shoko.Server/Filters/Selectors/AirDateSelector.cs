using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class AirDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the first date that a filterable aired";

    public override DateTime? Evaluate(IFilterable f)
    {
        return f.AirDate;
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
