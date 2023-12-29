using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.DateSelectors;

public class LastAddedDateSelector : FilterExpression<DateTime?>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the last date that any episode was added in a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override DateTime? Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.LastAddedDate;
    }

    protected bool Equals(LastAddedDateSelector other)
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

        return Equals((LastAddedDateSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(LastAddedDateSelector left, LastAddedDateSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LastAddedDateSelector left, LastAddedDateSelector right)
    {
        return !Equals(left, right);
    }
}
