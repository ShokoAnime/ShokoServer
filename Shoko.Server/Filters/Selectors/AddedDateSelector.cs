using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class AddedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the date that a filterable was created";

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AddedDate;
    }

    protected bool Equals(AddedDateSelector other)
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

        return Equals((AddedDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(AddedDateSelector left, AddedDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AddedDateSelector left, AddedDateSelector right)
    {
        return !Equals(left, right);
    }
}
